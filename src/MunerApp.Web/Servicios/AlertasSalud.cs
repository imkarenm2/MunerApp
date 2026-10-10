using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Constantes;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;

namespace MunerApp.Web.Servicios;

/// <summary>
/// HU-039: alertas de salud dentro de la plataforma para los administradores y los voluntarios de salud.
/// Escenario 1: medicamentos por vencer dentro del plazo configurado (revisión diaria).
/// Escenario 2: stock bajo al actualizar la cantidad (lo llama <see cref="InventarioMedicamentos"/>).
/// Escenario 3: recordatorio de los eventos de la agenda del día siguiente (revisión diaria).
/// Cada aviso se da una sola vez: se marca en el medicamento o en el evento en el mismo guardado de la notificación.
/// Las consultas ignoran el filtro por ESAL y filtran explícitamente, porque la revisión diaria no tiene usuario.
/// </summary>
public class AlertasSalud : ITareaDiaria
{
    private const string UrlInventario = "/Fundacion/Medicamentos";
    private const string UrlAgenda = "/Fundacion/Agenda";
    private static readonly CultureInfo Co = new("es-CO");

    private readonly MunerAppDbContext _db;
    private readonly INotificacionService _notificaciones;

    public AlertasSalud(MunerAppDbContext db, INotificacionService notificaciones)
    {
        _db = db;
        _notificaciones = notificaciones;
    }

    public string Nombre => "AlertasSalud";

    /// <summary>Revisión diaria: todas las fundaciones con el módulo de salud activo.</summary>
    public async Task<string> EjecutarAsync(DateTime hoy, CancellationToken ct)
    {
        var esales = await _db.EsalModulos.IgnoreQueryFilters().AsNoTracking()
            .Where(em => em.Activo && em.Modulo!.Codigo == CodigosModulo.Salud && em.Esal!.Activa)
            .Select(em => em.EsalId).Distinct().ToListAsync(ct);

        int vencimientos = 0, recordatorios = 0;
        foreach (var esalId in esales)
        {
            var r = await RevisarEsalAsync(esalId, hoy, ct);
            vencimientos += r.MedicamentosPorVencer;
            recordatorios += r.EventosManana;
        }
        return $"{esales.Count} fundaciones · {vencimientos} medicamentos por vencer avisados · {recordatorios} eventos de mañana recordados";
    }

    /// <summary>Escenarios 1 y 3 para una fundación. <paramref name="hoy"/> es la fecha en hora de Colombia.</summary>
    public async Task<ResultadoRevisionSalud> RevisarEsalAsync(int esalId, DateTime hoy, CancellationToken ct = default)
    {
        hoy = hoy.Date;
        var dias = await DiasAvisoAsync(_db, esalId, ct);

        // Escenario 1: por vencer dentro del plazo y sin aviso previo de esa fecha
        var limite = hoy.AddDays(dias);
        var porVencer = await _db.Medicamentos.IgnoreQueryFilters()
            .Where(m => m.EsalId == esalId && m.FechaVencimiento >= hoy && m.FechaVencimiento <= limite
                        && (m.VencimientoAvisado == null || m.VencimientoAvisado != m.FechaVencimiento))
            .OrderBy(m => m.FechaVencimiento).ThenBy(m => m.NombreComercial)
            .ToListAsync(ct);
        if (porVencer.Count > 0)
        {
            var (titulo, mensaje) = MensajeVencimiento(porVencer, hoy, dias);
            await _notificaciones.AgregarAResponsablesSaludAsync(esalId, titulo, mensaje, UrlInventario, "bi-hourglass-split");
            foreach (var m in porVencer) m.VencimientoAvisado = m.FechaVencimiento.Date;
        }

        // Escenario 3: eventos pendientes de mañana (día completo en hora de Colombia)
        var desde = Formatos.Utc(hoy.AddDays(1));
        var hasta = Formatos.Utc(hoy.AddDays(2));
        var manana = await _db.EventosAgenda.IgnoreQueryFilters()
            .Include(e => e.Beneficiario)
            .Where(e => e.EsalId == esalId && e.Estado == EstadoEventoAgenda.Pendiente && !e.RecordatorioEnviado
                        && e.FechaProgramada >= desde && e.FechaProgramada < hasta)
            .OrderBy(e => e.FechaProgramada)
            .ToListAsync(ct);
        if (manana.Count > 0)
        {
            var (titulo, mensaje) = MensajeRecordatorio(manana, hoy.AddDays(1));
            await _notificaciones.AgregarAResponsablesSaludAsync(esalId, titulo, mensaje, UrlAgenda, "bi-calendar-heart");
            foreach (var e in manana) e.RecordatorioEnviado = true;
        }

        // Las notificaciones y las marcas se guardan juntas: o queda todo o nada (no hay avisos repetidos ni perdidos)
        await _db.SaveChangesAsync(ct);
        return new ResultadoRevisionSalud(porVencer.Count, manana.Count);
    }

    /// <summary>
    /// Escenario 2: si un uso dejó la cantidad en el mínimo o por debajo (o la agotó), avisa a los responsables.
    /// Agrega la notificación al contexto; se guarda con el movimiento.
    /// </summary>
    public async Task AvisarSiBajoAsync(Medicamento m, decimal? minima, decimal antes, decimal despues)
    {
        var unidad = Textos.UnidadDe(m.Presentacion);
        string titulo, mensaje;
        if (despues <= 0 && antes > 0)
        {
            titulo = $"Se agotó {m.NombreComercial}";
            mensaje = $"Ya no quedan {unidad} de {m.NombreComercial}. Regístralo como entrada cuando llegue más.";
        }
        else if (Medicamento.CruzoMinimo(minima, antes, despues))
        {
            titulo = $"Stock bajo: {m.NombreComercial}";
            mensaje = $"Quedan {Formatos.Cantidad(despues)} {unidad} y el mínimo es {Formatos.Cantidad(minima!.Value)}. Conviene reponerlo.";
        }
        else return;

        await _notificaciones.AgregarAResponsablesSaludAsync(m.EsalId, titulo, mensaje, $"{UrlInventario}/Detalle/{m.Id}", "bi-arrow-down-circle");
    }

    /// <summary>Plazo de aviso de vencimiento configurado por la fundación (o el predeterminado).</summary>
    public static async Task<int> DiasAvisoAsync(MunerAppDbContext db, int esalId, CancellationToken ct = default)
        => await db.ConfigSalud.IgnoreQueryFilters().Where(c => c.EsalId == esalId)
               .Select(c => (int?)c.DiasAvisoVencimiento).FirstOrDefaultAsync(ct)
           ?? Medicamento.DiasPorVencerPredeterminado;

    /// <summary>Lo que el panel muestra en vivo a los responsables de la salud.</summary>
    public async Task<ResumenAlertasSalud> ResumenAsync(int esalId)
    {
        var ahora = DateTime.UtcNow;
        var hoy = Formatos.Local(ahora).Date;
        var dias = await DiasAvisoAsync(_db, esalId);
        var limite = hoy.AddDays(dias);
        var finHoy = Formatos.Utc(hoy.AddDays(1));
        var finManana = Formatos.Utc(hoy.AddDays(2));

        var medicamentos = _db.Medicamentos.IgnoreQueryFilters().Where(m => m.EsalId == esalId);
        var pendientes = _db.EventosAgenda.IgnoreQueryFilters().Where(e => e.EsalId == esalId && e.Estado == EstadoEventoAgenda.Pendiente);
        return new ResumenAlertasSalud(
            DiasAviso: dias,
            PorVencer: await medicamentos.CountAsync(m => m.FechaVencimiento >= hoy && m.FechaVencimiento <= limite),
            Vencidos: await medicamentos.CountAsync(m => m.FechaVencimiento < hoy),
            StockBajo: await medicamentos.CountAsync(m => m.CantidadMinima != null && m.Cantidad <= m.CantidadMinima),
            EventosAtrasados: await pendientes.CountAsync(e => e.FechaProgramada < ahora),
            EventosHoy: await pendientes.CountAsync(e => e.FechaProgramada >= ahora && e.FechaProgramada < finHoy),
            EventosManana: await pendientes.CountAsync(e => e.FechaProgramada >= finHoy && e.FechaProgramada < finManana));
    }

    // ---------- Textos de los avisos ----------

    private static (string Titulo, string Mensaje) MensajeVencimiento(List<Medicamento> meds, DateTime hoy, int dias)
    {
        static string Cuando(DateTime fecha, DateTime hoy)
        {
            var faltan = (fecha.Date - hoy).Days;
            return faltan switch { 0 => "hoy", 1 => "mañana", _ => $"en {faltan} días" };
        }

        if (meds.Count == 1)
        {
            var m = meds[0];
            return ("Medicamento por vencer",
                $"{m.NombreComercial} vence {Cuando(m.FechaVencimiento, hoy)} ({Formatos.Fecha(m.FechaVencimiento)}). " +
                $"Hay {Formatos.Cantidad(m.Cantidad)} {Textos.UnidadDe(m.Presentacion)}.");
        }

        const int MaxNombres = 4;
        var nombres = meds.Take(MaxNombres).Select(m => $"{m.NombreComercial} ({m.FechaVencimiento.ToString("d MMM", Co).TrimEnd('.')})").ToList();
        var lista = Unir(nombres) + (meds.Count > MaxNombres ? $" y {meds.Count - MaxNombres} más" : "");
        return ($"{meds.Count} medicamentos por vencer", $"Vencen en los próximos {dias} días: {lista}.");
    }

    private static (string Titulo, string Mensaje) MensajeRecordatorio(List<EventoAgenda> eventos, DateTime manana)
    {
        string Linea(EventoAgenda e) =>
            $"{Formatos.Hora(e.FechaProgramada)} {Textos.De(e.Tipo).ToLower(Co)} de {e.Beneficiario?.Nombre}" +
            (e.EsRecurrente ? $" (dosis {e.NumeroDosis} de {e.TotalDosis})" : "");

        var dia = manana.ToString("dddd d 'de' MMMM", Co);
        if (eventos.Count == 1)
            return ($"Mañana: {Textos.De(eventos[0].Tipo).ToLower(Co)} de {eventos[0].Beneficiario?.Nombre}",
                $"{char.ToUpper(dia[0], Co)}{dia[1..]}, {Linea(eventos[0])}: {eventos[0].Descripcion}");

        const int MaxLineas = 5;
        var lineas = eventos.Take(MaxLineas).Select(Linea).ToList();
        var lista = string.Join(" · ", lineas) + (eventos.Count > MaxLineas ? $" · y {eventos.Count - MaxLineas} más" : "");
        return ($"Mañana hay {eventos.Count} eventos de salud", $"{char.ToUpper(dia[0], Co)}{dia[1..]}: {lista}.");
    }

    private static string Unir(IReadOnlyList<string> partes) =>
        partes.Count <= 1 ? string.Join("", partes) : string.Join(", ", partes.Take(partes.Count - 1)) + " y " + partes[^1];
}

public record ResultadoRevisionSalud(int MedicamentosPorVencer, int EventosManana);

public record ResumenAlertasSalud(int DiasAviso, int PorVencer, int Vencidos, int StockBajo,
    int EventosAtrasados, int EventosHoy, int EventosManana)
{
    public bool HayAlgo => PorVencer + Vencidos + StockBajo + EventosAtrasados + EventosHoy + EventosManana > 0;
}
