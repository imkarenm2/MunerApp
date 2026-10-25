using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Exportacion;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Constantes;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Areas.Veeduria.Models;
using MunerApp.Web.Servicios;

namespace MunerApp.Web.Areas.Veeduria.Controllers;

/// <summary>
/// Veeduría ciudadana, capa pública: cualquier persona, sin cuenta, consulta lo que reciben las fundaciones y en qué
/// se usa. Nunca muestra nombres, correos ni documentos de los donantes: solo códigos, fechas, valores y evidencias.
/// El registro de cambios (quién modificó qué) es solo para veedores acreditados (RegistroCambiosController).
/// Como el visitante no tiene fundación, todas las consultas usan IgnoreQueryFilters() y filtran por la fundación.
/// </summary>
[Area("Veeduria")]
[AllowAnonymous]
public class ConsultaController : Controller
{
    private static readonly CultureInfo Co = new("es-CO");
    private readonly MunerAppDbContext _db;
    private readonly IAlmacenamientoArchivos _archivos;
    private readonly LibroFondos _libro;

    public ConsultaController(MunerAppDbContext db, IAlmacenamientoArchivos archivos, LibroFondos libro)
    {
        _db = db;
        _archivos = archivos;
        _libro = libro;
    }

    [HttpGet("veeduria")]
    public async Task<IActionResult> Index()
    {
        var desde = DateTime.UtcNow.AddYears(-1);
        var hace7 = DateTime.UtcNow.AddDays(-7);
        var hoy = Formatos.Hoy();
        var esales = await _db.Esales.IgnoreQueryFilters().AsNoTracking().Where(e => e.Activa).OrderBy(e => e.Nombre)
            .Select(e => new FundacionVeeduriaItem
            {
                Id = e.Id,
                Nombre = e.Nombre,
                Ciudad = e.Ciudad,
                Nit = e.Nit,
                DineroConfirmado = _db.Donaciones.Where(d => d.EsalId == e.Id && d.Estado == EstadoDonacion.Confirmada && d.FechaRevision >= desde).Sum(d => (decimal?)d.Valor) ?? 0,
                DonacionesDinero = _db.Donaciones.Count(d => d.EsalId == e.Id && d.Estado == EstadoDonacion.Confirmada && d.FechaRevision >= desde),
                DonacionesEspecie = _db.DonacionesEspecie.Count(d => d.EsalId == e.Id && d.Estado == EstadoDonacionEspecie.Recibida && d.FechaRecepcion >= desde),
                DonacionesSinRevisar = _db.Donaciones.Count(d => d.EsalId == e.Id && d.Estado == EstadoDonacion.Pendiente && d.FechaReporte < hace7),
                DocumentosPublicados = _db.DocumentosTransparencia.Count(d => d.EsalId == e.Id && d.Visible),
                GastadoConSoporte = _db.MovimientosFondo.Where(m => m.EsalId == e.Id && !m.Anulado && m.Tipo == TipoMovimientoFondo.Gasto && m.FechaRegistro >= desde)
                    .Sum(m => (decimal?)m.Valor) ?? 0,
                // Causas que ya terminaron su plazo y no tienen rendición de cuentas
                CausasCerradasSinRendicion = _db.Causas.Count(c => c.EsalId == e.Id && c.RendicionDocumentoId == null
                    && (c.Estado == EstadoCausa.Cerrada || ((c.Estado == EstadoCausa.Activa || c.Estado == EstadoCausa.Pausada) && c.FechaLimite < hoy)))
            })
            .ToListAsync();
        return View(esales);
    }

    [HttpGet("veeduria/{id:int}")]
    public async Task<IActionResult> Fundacion(int id, int? anio)
    {
        var modelo = await CargarAsync(id, anio);
        return modelo is null ? NotFound() : View(modelo);
    }

    /// <summary>Descarga en CSV (se abre en Excel) para que la veeduría haga sus propios cruces.</summary>
    [HttpGet("veeduria/{id:int}/csv")]
    public async Task<IActionResult> Csv(int id, int? anio, string tipo = "dinero")
    {
        var m = await CargarAsync(id, anio);
        if (m is null) return NotFound();

        var sb = new StringBuilder();
        // Celdas protegidas contra inyección de fórmulas en Excel (ver ArchivoCsv.Celda)
        static string C(string? v) => ArchivoCsv.Celda(v);
        switch (tipo)
        {
            case "especie":
                sb.AppendLine("Codigo;Fecha de recepcion;Articulo;Tipo;Cantidad;Unidad;Vence;Usado;Disponible");
                foreach (var e in m.Especie)
                    sb.AppendLine(string.Join(';', C(e.Codigo), e.Fecha.ToString("yyyy-MM-dd"), C(e.Articulo), C(e.Categoria), e.Cantidad, C(e.Unidad), e.Vence?.ToString("yyyy-MM-dd") ?? "",
                        e.Usado, e.Disponible));
                break;
            case "movimientos":
                sb.AppendLine("Fecha;Fondo;Tipo;Concepto;Proveedor;Valor;Otro fondo;Soporte;Anulado;Motivo de anulacion");
                var raiz = $"{Request.Scheme}://{Request.Host}";
                foreach (var x in m.Movimientos)
                    sb.AppendLine(string.Join(';', x.Fecha.ToString("yyyy-MM-dd"), C(x.Fondo), C(Textos.De(x.Tipo)), C(x.Concepto), C(x.Proveedor),
                        (x.EsSalida ? -x.Valor : x.Valor).ToString("0", Co), C(x.FondoRelacionado), C(x.SoporteUrl is null ? null : raiz + x.SoporteUrl),
                        x.Anulado ? "Si" : "No", C(x.MotivoAnulacion)));
                break;
            case "causas":
                sb.AppendLine("Causa;Estado;Meta;Recaudado;Fecha limite;Rendicion de cuentas");
                foreach (var c in m.Causas)
                    sb.AppendLine(string.Join(';', C(c.Titulo), C(c.Cerrada ? "Cerrada" : Textos.De(c.Estado)), c.Meta.ToString("0", Co), c.Recaudado.ToString("0", Co),
                        c.FechaLimite.ToString("yyyy-MM-dd"), C(c.RendicionUrl is null ? "Sin publicar" : "Publicada")));
                break;
            default:
                sb.AppendLine("Codigo;Fecha de confirmacion;Valor;Destino;Medio");
                foreach (var d in m.Dinero)
                    sb.AppendLine(string.Join(';', C(d.Codigo), d.Fecha.ToString("yyyy-MM-dd"), d.Valor.ToString("0", Co), C(d.Destino), C(d.Medio)));
                break;
        }
        // BOM para que Excel reconozca las tildes
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"veeduria-{m.Slug ?? m.Id.ToString()}-{tipo}-{m.Anio}.csv");
    }

    private async Task<FundacionVeeduriaViewModel?> CargarAsync(int id, int? anio)
    {
        var esal = await _db.Esales.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(e => e.Id == id && e.Activa);
        if (esal is null) return null;

        var anioActual = Formatos.Hoy().Year;
        var a = anio is int x && x >= 2024 && x <= anioActual ? x : anioActual;
        var desde = Formatos.Utc(new DateTime(a, 1, 1));
        var hasta = Formatos.Utc(new DateTime(a + 1, 1, 1));
        var hoy = Formatos.Hoy();

        var confirmadas = _db.Donaciones.IgnoreQueryFilters().AsNoTracking()
            .Where(d => d.EsalId == id && d.Estado == EstadoDonacion.Confirmada && d.FechaRevision >= desde && d.FechaRevision < hasta);

        var dinero = await confirmadas.OrderByDescending(d => d.FechaRevision)
            .Select(d => new
            {
                d.Codigo, Fecha = d.FechaRevision!.Value, d.Valor, d.Origen,
                Causa = d.Causa != null ? d.Causa.Titulo : null,
                Apadrinamiento = d.ApadrinamientoId != null
            }).ToListAsync();
        string Destino(string? causa, bool apadrinamiento) => causa is not null ? "Causa: " + causa : apadrinamiento ? "Apadrinamiento" : "Donación general";

        var revisiones = await _db.Donaciones.IgnoreQueryFilters().AsNoTracking()
            .Where(d => d.EsalId == id && d.FechaRevision != null && d.FechaRevision >= desde && d.FechaRevision < hasta)
            .Select(d => new { d.FechaReporte, d.FechaRevision }).ToListAsync();

        var publicos = Causa.EstadosPublicos;
        var causas = await _db.Causas.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.EsalId == id && publicos.Contains(c.Estado) && c.FechaCreacion < hasta && c.FechaLimite >= new DateTime(a, 1, 1))
            .OrderByDescending(c => c.FechaLimite)
            .Select(c => new
            {
                c.Id, c.Titulo, c.Estado, c.Meta, c.FechaLimite, c.RendicionDocumentoId,
                Recaudado = _db.Donaciones.Where(d => d.CausaId == c.Id && d.Estado == EstadoDonacion.Confirmada).Sum(d => (decimal?)d.Valor) ?? 0
            }).ToListAsync();
        var docIds = causas.Where(c => c.RendicionDocumentoId != null).Select(c => c.RendicionDocumentoId!.Value).ToList();
        var rendiciones = await _db.DocumentosTransparencia.IgnoreQueryFilters().AsNoTracking().Where(d => docIds.Contains(d.Id) && d.Visible)
            .ToDictionaryAsync(d => d.Id, d => d.Ruta);

        var especie = await _db.DonacionesEspecie.IgnoreQueryFilters().AsNoTracking()
            .Where(d => d.EsalId == id && d.Estado == EstadoDonacionEspecie.Recibida && d.FechaRecepcion >= desde && d.FechaRecepcion < hasta)
            .OrderByDescending(d => d.FechaRecepcion)
            .Select(d => new { d.Id, d.Codigo, Fecha = d.FechaRecepcion!.Value, d.Articulo, d.Categoria, Cantidad = d.CantidadRecibida ?? 0, d.Unidad, d.FechaVencimiento, d.FotoRecepcionRuta })
            .ToListAsync();

        var lotes = especie.Select(e => e.Id).ToList();
        var usos = (await _db.SalidasEspecie.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.EsalId == id && lotes.Contains(s.DonacionEspecieId))
                .OrderByDescending(s => s.Fecha).ThenByDescending(s => s.Id)
                .Select(s => new { s.DonacionEspecieId, s.Cantidad, s.Uso, s.Fecha, s.FotoRuta, s.Anulado, s.MotivoAnulacion })
                .ToListAsync())
            .ToLookup(s => s.DonacionEspecieId,
                s => new MunerApp.Web.Models.Publico.UsoEspeciePublico(s.Cantidad, s.Uso, s.Fecha,
                    s.FotoRuta is null ? null : _archivos.UrlPublica(s.FotoRuta), s.Anulado, s.MotivoAnulacion));

        var documentos = await _db.DocumentosTransparencia.IgnoreQueryFilters().AsNoTracking().Where(d => d.EsalId == id && d.Visible)
            .OrderByDescending(d => d.FechaPublicacion)
            .Select(d => new { d.Titulo, d.Categoria, d.FechaPublicacion, d.Ruta }).ToListAsync();

        var primerAnio = await _db.Donaciones.IgnoreQueryFilters().AsNoTracking().Where(d => d.EsalId == id).MinAsync(d => (DateTime?)d.FechaReporte);
        var inicio = Math.Max(2024, primerAnio?.Year ?? anioActual);

        return new FundacionVeeduriaViewModel
        {
            Id = esal.Id,
            Nombre = esal.Nombre,
            Nit = esal.Nit,
            Ciudad = esal.Ciudad,
            Slug = esal.Slug,
            Anio = a,
            Anios = Enumerable.Range(inicio, anioActual - inicio + 1).Reverse().ToList(),
            DineroConfirmado = dinero.Sum(d => d.Valor),
            DonacionesDinero = dinero.Count,
            DonacionesPendientes = await _db.Donaciones.IgnoreQueryFilters().CountAsync(d => d.EsalId == id && d.Estado == EstadoDonacion.Pendiente),
            PendientesMasDeUnaSemana = await _db.Donaciones.IgnoreQueryFilters().CountAsync(d => d.EsalId == id && d.Estado == EstadoDonacion.Pendiente && d.FechaReporte < DateTime.UtcNow.AddDays(-7)),
            DiasPromedioRevision = revisiones.Count == 0 ? null : Math.Round(revisiones.Average(r => (r.FechaRevision!.Value - r.FechaReporte).TotalDays), 1),
            EspecieRecibidas = especie.Count,
            EspecieNoRecibidas = await _db.DonacionesEspecie.IgnoreQueryFilters().CountAsync(d => d.EsalId == id && d.Estado == EstadoDonacionEspecie.NoRecibida && d.FechaRecepcion >= desde && d.FechaRecepcion < hasta),
            PorDestino = dinero.GroupBy(d => d.Causa is not null ? "Causas" : d.Apadrinamiento ? "Apadrinamientos" : "Donación general")
                .Select(g => (g.Key, g.Sum(d => d.Valor), g.Count())).OrderByDescending(g => g.Item2).ToList(),
            Causas = causas.Select(c => new CausaVeeduria(c.Id, c.Titulo, c.Estado,
                c.Estado == EstadoCausa.Cerrada || c.Recaudado >= c.Meta || c.FechaLimite < hoy, c.Meta, c.Recaudado, c.FechaLimite,
                c.RendicionDocumentoId is int r && rendiciones.TryGetValue(r, out var ruta) ? _archivos.UrlPublica(ruta) : null)).ToList(),
            Dinero = dinero.Select(d => new DonacionVeeduria(d.Codigo, Formatos.Local(d.Fecha), d.Valor, Destino(d.Causa, d.Apadrinamiento),
                d.Origen == OrigenDonacion.Wompi ? "En línea (Wompi)" : "Transferencia")).ToList(),
            Especie = especie.Select(d => new EspecieVeeduria(d.Codigo, Formatos.Local(d.Fecha), d.Articulo, Textos.De(d.Categoria), d.Cantidad, d.Unidad,
                d.FechaVencimiento, d.FotoRecepcionRuta is null ? null : _archivos.UrlPublica(d.FotoRecepcionRuta), usos[d.Id].ToList())).ToList(),
            Documentos = documentos.Select(d => new DocumentoVeeduria(d.Titulo, Textos.De(d.Categoria), d.FechaPublicacion, _archivos.UrlPublica(d.Ruta))).ToList(),
            Fondos = await _libro.ResumenAsync(id),
            // Fecha del gasto en hora local (se guarda como día), así que se filtra por el año calendario
            Movimientos = await _libro.MovimientosAsync(id, desde: new DateTime(a, 1, 1), hasta: new DateTime(a + 1, 1, 1))
        };
    }
}
