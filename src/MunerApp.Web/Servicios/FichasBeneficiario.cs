using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Application.Seguridad;
using MunerApp.Domain.Constantes;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;

namespace MunerApp.Web.Servicios;

/// <summary>Un documento listo para descargar.</summary>
public record DocumentoDescarga(byte[] Contenido, string NombreArchivo);

/// <summary>
/// Hoja de vida (HU-017) e historia clínica (HU-019) de un beneficiario en PDF. Arma los datos con las mismas
/// reglas de la pantalla: la parte clínica solo va si el usuario tiene acceso clínico, y nunca van los datos
/// personales del adoptante ni de los padrinos. Cada descarga queda en el registro de auditoría.
/// Las consultas usan el filtro global por ESAL: nunca se descarga un beneficiario de otra fundación.
/// </summary>
public class FichasBeneficiario
{
    private readonly MunerAppDbContext _db;
    private readonly IAlmacenamientoArchivos _archivos;
    private readonly IReportesService _reportes;

    public FichasBeneficiario(MunerAppDbContext db, IAlmacenamientoArchivos archivos, IReportesService reportes)
    {
        _db = db;
        _archivos = archivos;
        _reportes = reportes;
    }

    /// <summary>Hoja de vida en PDF, o null si el beneficiario no existe (o es de otra fundación).</summary>
    public async Task<DocumentoDescarga?> HojaDeVidaAsync(int id, bool incluirClinica, ClaimsPrincipal usuario)
    {
        var b = await _db.Beneficiarios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return null;

        var registrador = await NombreAsync(b.RegistradoPorId);
        var secciones = new List<SeccionFicha>
        {
            new("Datos generales", Campos(
                ("Especie", b.Especie),
                ("Raza", b.Raza),
                ("Sexo", Textos.De(b.Sexo)),
                ("Color", b.Color),
                ("Edad", EdadTexto(b)),
                ("Fecha de ingreso", Formatos.Fecha(b.FechaRescate)),
                ("Peso al ingreso", b.PesoIngresoKg is decimal peso ? $"{Formatos.Numero(peso)} kg" : null),
                ("Estado actual", Textos.De(b.Estado)),
                ("Registrado", Formatos.FechaCorta(b.FechaRegistro) + (registrador is null ? "" : " por " + registrador)))),
            new("Procedencia y reseña", Campos(
                ("Procedencia", b.Procedencia is Procedencia p ? Textos.De(p) : null),
                ("Estado reproductivo", b.EstadoReproductivo is EstadoReproductivo er ? Textos.De(er) : null),
                ("Detalles de la procedencia", b.DetallesProcedencia),
                ("Señales particulares", b.SenalesParticulares)))
        };
        if (incluirClinica) secciones.Add(await ExamenIngresoAsync(b));

        var historial = await (from h in _db.HistorialEstadosBeneficiario.AsNoTracking()
                               where h.BeneficiarioId == id
                               join u in _db.Users on h.CambiadoPorId equals u.Id into us
                               from u in us.DefaultIfEmpty()
                               orderby h.Fecha, h.Id
                               select new { h.Fecha, h.Estado, h.Nota, Por = u == null ? null : u.NombreCompleto })
            .ToListAsync();
        var tablas = new List<TablaFicha>
        {
            new("Historial de estados",
                new[] { "Fecha", "Estado", "Nota", "Registró" },
                new[] { 1.4f, 1.3f, 3f, 1.6f },
                historial.Select(h => (IReadOnlyList<string>)new[] { Formatos.FechaHora(h.Fecha), Textos.De(h.Estado), h.Nota ?? "", h.Por ?? "" }).ToList(),
                "Todavía no hay cambios de estado.")
        };

        return await ArmarAsync("Hoja de vida", b, secciones, tablas, usuario);
    }

    /// <summary>Historia clínica en PDF: examen de ingreso y todos los eventos en orden cronológico.</summary>
    public async Task<DocumentoDescarga?> HistoriaClinicaAsync(int id, ClaimsPrincipal usuario)
    {
        var b = await _db.Beneficiarios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return null;

        var secciones = new List<SeccionFicha>
        {
            new("Datos del paciente", Campos(
                ("Especie", b.Especie),
                ("Raza", b.Raza),
                ("Sexo", Textos.De(b.Sexo)),
                ("Color", b.Color),
                ("Edad", EdadTexto(b)),
                ("Fecha de ingreso", Formatos.Fecha(b.FechaRescate)),
                ("Peso al ingreso", b.PesoIngresoKg is decimal peso ? $"{Formatos.Numero(peso)} kg" : null),
                ("Estado reproductivo", b.EstadoReproductivo is EstadoReproductivo er ? Textos.De(er) : null))),
            await ExamenIngresoAsync(b)
        };

        var eventos = await _db.EventosClinicos.AsNoTracking()
            .Where(e => e.BeneficiarioId == id)
            .OrderBy(e => e.Fecha).ThenBy(e => e.Id)
            .ToListAsync();
        var filas = eventos.Select(e =>
        {
            var detalle = new List<string>();
            if (!string.IsNullOrWhiteSpace(e.Producto)) detalle.Add(e.Producto);
            if (!string.IsNullOrWhiteSpace(e.Laboratorio)) detalle.Add("Lab. " + e.Laboratorio);
            if (!string.IsNullOrWhiteSpace(e.Resultado)) detalle.Add("Resultado: " + e.Resultado);
            if (e.PesoKg is decimal pk) detalle.Add($"Peso: {Formatos.Numero(pk)} kg");
            return (IReadOnlyList<string>)new[]
            {
                Formatos.Fecha(e.Fecha), Textos.De(e.Tipo), string.Join(" · ", detalle), e.Descripcion, e.Responsable
            };
        }).ToList();

        var tablas = new List<TablaFicha>
        {
            new("Vacunas, tratamientos y controles",
                new[] { "Fecha", "Tipo", "Producto y resultado", "Descripción", "Responsable" },
                new[] { 1.5f, 1.3f, 2f, 3f, 1.5f },
                filas,
                "Todavía no hay eventos en la historia clínica.")
        };

        return await ArmarAsync("Historia clínica", b, secciones, tablas, usuario);
    }

    private async Task<DocumentoDescarga> ArmarAsync(string tipo, Beneficiario b, List<SeccionFicha> secciones,
        List<TablaFicha> tablas, ClaimsPrincipal usuario)
    {
        var esal = await _db.Esales.AsNoTracking().Where(e => e.Id == b.EsalId).Select(e => new { e.Nombre, e.Nit }).FirstAsync();
        var quien = usuario.FindFirst(MunerAppClaims.NombreCompleto)?.Value ?? usuario.Identity?.Name ?? "un usuario de la fundación";

        var pdf = _reportes.GenerarFichaBeneficiario(new DatosFichaBeneficiario(
            tipo, esal.Nombre, esal.Nit, b.Nombre,
            $"{b.Especie} · {Textos.De(b.Sexo)} · {b.Color} · {Formatos.Edad(b.FechaNacimiento)} · {Textos.De(b.Estado)}",
            await FotoAsync(b.FotoRuta), secciones, tablas, quien, DateTime.UtcNow));

        await RegistrarDescargaAsync(b, tipo, usuario, quien);
        return new DocumentoDescarga(pdf, $"{tipo.Replace(' ', '-')}-{NombreArchivo(b.Nombre)}-{Formatos.Hoy():yyyy-MM-dd}.pdf");
    }

    /// <summary>Datos clínicos: quien los descarga también deja rastro, como el veedor al consultar.</summary>
    private async Task RegistrarDescargaAsync(Beneficiario b, string tipo, ClaimsPrincipal usuario, string quien)
    {
        _db.RegistrosAuditoria.Add(new RegistroAuditoria
        {
            UsuarioId = usuario.FindFirstValue(ClaimTypes.NameIdentifier),
            UsuarioNombre = quien,
            EsalId = b.EsalId,
            Entidad = "Beneficiario",
            EntidadId = b.Id.ToString(),
            Accion = AccionAuditoria.Consultado,
            Cambios = JsonSerializer.Serialize(new Dictionary<string, string> { ["Descarga"] = tipo + " en PDF" })
        });
        await _db.SaveChangesAsync();
    }

    private async Task<SeccionFicha> ExamenIngresoAsync(Beneficiario b)
    {
        if (b.FechaExamenIngreso is null)
            return new("Examen de ingreso", new[] { new CampoFicha("Examen", "Todavía no se ha registrado el examen de ingreso.") });

        var realizo = await NombreAsync(b.ExamenIngresoPorId);
        return new("Examen de ingreso", Campos(
            ("Fecha del examen", Formatos.FechaCorta(b.FechaExamenIngreso.Value)),
            ("Realizado por", realizo),
            ("Frecuencia respiratoria", b.FrecuenciaRespiratoria is int fr ? $"{fr} rpm" : null),
            ("Frecuencia cardiaca", b.FrecuenciaCardiaca is int fc ? $"{fc} lpm" : null),
            ("TLLC", b.Tllc is decimal t ? $"{Formatos.Numero(t)} s" : null),
            ("RPC", b.Rpc is decimal r ? $"{Formatos.Numero(r)} s" : null),
            ("Temperatura", b.Temperatura is decimal tmp ? $"{Formatos.Numero(tmp)} °C" : null),
            ("Condición corporal", b.CondicionCorporal is int cc ? $"{cc} de 5" : null),
            ("Mucosa conjuntival", b.MucosaConjuntival),
            ("Mucosa oral", b.MucosaOral),
            ("Mucosa rectal", b.MucosaRectal),
            ("Mucosa vulvar o prepucial", b.MucosaVulvarPrepucial),
            ("Estado de conciencia", b.EstadoConciencia is EstadoConciencia ec ? Textos.De(ec) : null),
            ("Observaciones", b.ObservacionesIngreso)));
    }

    /// <summary>Solo los campos con valor: un dato vacío no aporta nada en el papel.</summary>
    private static IReadOnlyList<CampoFicha> Campos(params (string Etiqueta, string? Valor)[] campos)
        => campos.Where(c => !string.IsNullOrWhiteSpace(c.Valor)).Select(c => new CampoFicha(c.Etiqueta, c.Valor!.Trim())).ToList();

    private static string EdadTexto(Beneficiario b) => Formatos.Edad(b.FechaNacimiento)
        + (b.FechaNacimientoExacta ? $" (nació el {Formatos.Fecha(b.FechaNacimiento)})" : " (aprox.)");

    private async Task<string?> NombreAsync(string? usuarioId) => usuarioId is null ? null
        : await _db.Users.AsNoTracking().Where(u => u.Id == usuarioId).Select(u => u.NombreCompleto).FirstOrDefaultAsync();

    private async Task<byte[]?> FotoAsync(string? ruta)
    {
        if (ruta is null) return null;
        await using var stream = await _archivos.AbrirAsync(ruta);
        if (stream is null) return null;
        using var memoria = new MemoryStream();
        await stream.CopyToAsync(memoria);
        return memoria.ToArray();
    }

    /// <summary>"Señor Bigotes" → "Senor-Bigotes": nombre de archivo seguro, sin tildes ni símbolos.</summary>
    private static string NombreArchivo(string nombre)
    {
        var normal = nombre.Normalize(System.Text.NormalizationForm.FormD);
        var letras = normal.Where(c => char.IsAsciiLetterOrDigit(c) || c == ' ').ToArray();
        var limpio = string.Join('-', new string(letras).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return limpio.Length == 0 ? "beneficiario" : limpio;
    }
}
