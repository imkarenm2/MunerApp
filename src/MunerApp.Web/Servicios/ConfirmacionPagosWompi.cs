using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Infrastructure.Servicios;

namespace MunerApp.Web.Servicios;

/// <summary>
/// HU-044: las donaciones en línea se confirman solas con el aviso (evento) de Wompi.
/// Escenario 1: transacción aprobada con firma válida → la donación pasa a "Confirmada", suma al recaudado
/// de la causa (que se calcula con las donaciones confirmadas) y el donante ya puede descargar su comprobante.
/// Escenario 2: firma inválida → no se modifica ninguna donación y queda el registro del intento.
/// Escenario 3: evento repetido → la donación solo cambia si sigue "Pendiente" (actualización atómica), así nunca se procesa dos veces.
/// Cada aviso queda en <see cref="EventoPasarela"/>.
/// </summary>
public class ConfirmacionPagosWompi
{
    /// <summary>Lo que se responde a Wompi: 200 para que no reintente; 401 si la firma no es válida.</summary>
    public record Respuesta(ResultadoEventoPasarela Resultado, int CodigoHttp);

    private const int MaximoCuerpo = 20_000;

    private readonly MunerAppDbContext _db;
    private readonly ISecretosService _secretos;
    private readonly INotificacionService _notificaciones;
    private readonly ILogger<ConfirmacionPagosWompi> _logger;

    public ConfirmacionPagosWompi(MunerAppDbContext db, ISecretosService secretos, INotificacionService notificaciones,
        ILogger<ConfirmacionPagosWompi> logger)
    {
        _db = db;
        _secretos = secretos;
        _notificaciones = notificaciones;
        _logger = logger;
    }

    public async Task<Respuesta> ProcesarAsync(int esalId, string cuerpo, CancellationToken ct = default)
    {
        // La URL incluye la fundación: así se sabe con qué secreto validar la firma
        var esal = await _db.Esales.AsNoTracking().Where(e => e.Id == esalId).Select(e => new { e.Id, e.Nombre }).FirstOrDefaultAsync(ct);
        if (esal is null) return new(ResultadoEventoPasarela.Ignorado, StatusCodes.Status404NotFound);

        var registro = new EventoPasarela
        {
            EsalId = esalId,
            Cuerpo = cuerpo.Length > MaximoCuerpo ? cuerpo[..MaximoCuerpo] : cuerpo
        };

        JsonDocument documento;
        try
        {
            documento = JsonDocument.Parse(cuerpo);
        }
        catch (JsonException)
        {
            return await RegistrarAsync(registro, ResultadoEventoPasarela.Invalido, "El cuerpo no es JSON.", StatusCodes.Status400BadRequest, ct);
        }

        using (documento)
        {
            var evento = documento.RootElement;
            var transaccion = Buscar(evento, "data", "transaction");
            registro.Evento = Truncar(Cadena(evento, "event"), 60);
            registro.TransaccionId = Truncar(Cadena(transaccion, "id"), 60);
            registro.Referencia = Truncar(Cadena(transaccion, "reference"), 60);
            registro.EstadoTransaccion = Truncar(Cadena(transaccion, "status"), 20);

            var config = await _db.ConfigPasarelas.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(c => c.EsalId == esalId, ct);
            if (config is null || !config.Activa)
                return await RegistrarAsync(registro, ResultadoEventoPasarela.Ignorado, "La fundación no tiene pagos en línea activos.", StatusCodes.Status200OK, ct);

            string secreto;
            try
            {
                secreto = _secretos.Desproteger(config.SecretoEventosCifrado);
            }
            catch (CryptographicException ex)
            {
                _logger.LogError(ex, "No se pudo descifrar el secreto de eventos de Wompi de la ESAL {EsalId}.", esalId);
                return await RegistrarAsync(registro, ResultadoEventoPasarela.Ignorado, "No se pudo leer el secreto de eventos de la fundación.", StatusCodes.Status500InternalServerError, ct);
            }

            // Escenario 2: firma inválida → se rechaza sin tocar ninguna donación
            if (!WompiFirmaEventos.EsValida(evento, secreto))
            {
                _logger.LogWarning("Evento de Wompi con firma inválida para la ESAL {EsalId} (transacción {Transaccion}).", esalId, registro.TransaccionId);
                return await RegistrarAsync(registro, ResultadoEventoPasarela.FirmaInvalida, "La firma no coincide con el secreto de eventos.", StatusCodes.Status401Unauthorized, ct);
            }

            if (registro.Evento != "transaction.updated" || registro.TransaccionId is null || registro.Referencia is null)
                return await RegistrarAsync(registro, ResultadoEventoPasarela.Ignorado, "No es una actualización de transacción.", StatusCodes.Status200OK, ct);

            EstadoDonacion nuevo;
            switch (registro.EstadoTransaccion)
            {
                case "APPROVED": nuevo = EstadoDonacion.Confirmada; break;
                case "DECLINED" or "VOIDED" or "ERROR": nuevo = EstadoDonacion.Rechazada; break;
                default:
                    return await RegistrarAsync(registro, ResultadoEventoPasarela.Ignorado, $"Estado {registro.EstadoTransaccion}: la donación sigue pendiente.", StatusCodes.Status200OK, ct);
            }

            var donacion = await _db.Donaciones.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(d => d.EsalId == esalId && d.Origen == OrigenDonacion.Wompi && d.ReferenciaPasarela == registro.Referencia, ct);
            if (donacion is null)
                return await RegistrarAsync(registro, ResultadoEventoPasarela.DonacionNoEncontrada, "No hay una donación en línea con esa referencia.", StatusCodes.Status200OK, ct);
            registro.DonacionId = donacion.Id;

            // Escenario 3: ya procesada (por este aviso repetido o por otro) → no se suma dos veces
            if (donacion.Estado != EstadoDonacion.Pendiente)
                return await RegistrarAsync(registro, ResultadoEventoPasarela.Duplicado, $"La donación ya estaba {donacion.Estado}.", StatusCodes.Status200OK, ct);

            // El valor pagado debe ser el de la donación, en pesos colombianos
            var centavos = Buscar(transaccion, "amount_in_cents");
            var moneda = Cadena(transaccion, "currency");
            if (nuevo == EstadoDonacion.Confirmada
                && (centavos.ValueKind != JsonValueKind.Number || !centavos.TryGetInt64(out var valorCentavos)
                    || valorCentavos != (long)(donacion.Valor * 100) || moneda != "COP"))
                return await RegistrarAsync(registro, ResultadoEventoPasarela.MontoNoCoincide,
                    $"Wompi reporta {Cadena(transaccion, "amount_in_cents") ?? "?"} centavos {moneda ?? "?"}; la donación es por {donacion.Valor:0} COP.", StatusCodes.Status200OK, ct);

            var metodo = Cadena(transaccion, "payment_method_type");
            var medioPago = Truncar(string.IsNullOrEmpty(metodo) ? "Wompi" : $"Wompi · {metodo}", 200)!;
            var motivo = nuevo == EstadoDonacion.Rechazada ? $"Wompi no aprobó el pago ({registro.EstadoTransaccion})." : null;
            var ahora = DateTime.UtcNow;

            // Solo cambia si sigue pendiente: si dos avisos llegan a la vez, uno solo la actualiza
            int actualizadas;
            try
            {
                actualizadas = await _db.Donaciones.IgnoreQueryFilters()
                    .Where(d => d.Id == donacion.Id && d.Estado == EstadoDonacion.Pendiente)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(d => d.Estado, nuevo)
                        .SetProperty(d => d.TransaccionPasarelaId, registro.TransaccionId)
                        .SetProperty(d => d.MedioPago, medioPago)
                        .SetProperty(d => d.MotivoRechazo, motivo)
                        .SetProperty(d => d.FechaRevision, ahora), ct);
            }
            catch (DbUpdateException)
            {
                // La transacción ya está asociada a otra donación (índice único)
                actualizadas = 0;
            }
            if (actualizadas == 0)
                return await RegistrarAsync(registro, ResultadoEventoPasarela.Duplicado, "La donación cambió mientras se procesaba el aviso.", StatusCodes.Status200OK, ct);

            if (nuevo == EstadoDonacion.Confirmada)
            {
                _notificaciones.Agregar(donacion.DonanteId, "¡Tu donación en línea fue confirmada!",
                    $"Wompi confirmó tu donación {donacion.Codigo} por {Formatos.Pesos(donacion.Valor)} a {esal.Nombre}. Ya puedes descargar tu comprobante.",
                    $"/mis-donaciones/{donacion.Codigo}", "bi-check-circle");
                await _notificaciones.AgregarAAdministradoresAsync(esalId, "Donación en línea confirmada",
                    $"Wompi confirmó la donación {donacion.Codigo} por {Formatos.Pesos(donacion.Valor)}.",
                    "/Fundacion/Donaciones?estado=Confirmada", "bi-credit-card");
            }
            else
            {
                _notificaciones.Agregar(donacion.DonanteId, "Tu pago en línea no fue aprobado",
                    $"Wompi no aprobó el pago de tu donación {donacion.Codigo}. No se te cobró; puedes intentarlo de nuevo.",
                    $"/mis-donaciones/{donacion.Codigo}", "bi-x-circle");
            }

            return await RegistrarAsync(registro, ResultadoEventoPasarela.Procesado, $"Donación {donacion.Codigo}: {nuevo}.", StatusCodes.Status200OK, ct);
        }
    }

    private async Task<Respuesta> RegistrarAsync(EventoPasarela registro, ResultadoEventoPasarela resultado, string detalle, int codigo, CancellationToken ct)
    {
        registro.Resultado = resultado;
        registro.Detalle = Truncar(detalle, 300);
        _db.EventosPasarela.Add(registro);
        await _db.SaveChangesAsync(ct);
        return new(resultado, codigo);
    }

    private static JsonElement Buscar(JsonElement elemento, params string[] ruta)
    {
        foreach (var parte in ruta)
        {
            if (elemento.ValueKind != JsonValueKind.Object || !elemento.TryGetProperty(parte, out elemento))
                return default;
        }
        return elemento;
    }

    private static string? Cadena(JsonElement elemento, string propiedad)
    {
        var valor = Buscar(elemento, propiedad);
        return valor.ValueKind switch
        {
            JsonValueKind.String => valor.GetString(),
            JsonValueKind.Number => valor.GetRawText(),
            _ => null
        };
    }

    private static string? Truncar(string? texto, int maximo) => texto is null || texto.Length <= maximo ? texto : texto[..maximo];
}
