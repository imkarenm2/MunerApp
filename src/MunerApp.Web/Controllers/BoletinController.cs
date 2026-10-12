using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Models.Publico;
using MunerApp.Web.Servicios;

namespace MunerApp.Web.Controllers;

/// <summary>
/// HU-028: el visitante consulta el boletín y los eventos, sin iniciar sesión.
/// Escenario 1: boletín de cada fundación y general, con las publicaciones más recientes primero y filtro por categoría.
/// Escenario 2: los próximos eventos se destacan arriba, con fecha, hora y lugar; los pasados quedan marcados.
/// Escenario 3: el detalle muestra el contenido completo y, si es un evento futuro, permite agregarlo al calendario.
/// Solo muestra publicaciones publicadas de fundaciones activas: usa IgnoreQueryFilters y filtra de forma explícita.
/// Las acciones no se llaman Index para que los enlaces del área Fundacion (que tiene un BoletinController) no se confundan.
/// </summary>
public class BoletinController : Controller
{
    private const int PorPagina = 12;

    private readonly MunerAppDbContext _db;
    private readonly IAlmacenamientoArchivos _archivos;
    private readonly PeriodicoMunerApp _periodico;

    public BoletinController(MunerAppDbContext db, IAlmacenamientoArchivos archivos, PeriodicoMunerApp periodico)
    {
        _db = db;
        _archivos = archivos;
        _periodico = periodico;
    }

    [HttpGet("boletin")]
    public async Task<IActionResult> General(CategoriaPublicacion? categoria, int pagina = 1)
    {
        // En la portada del periódico los próximos eventos ya salen arriba: aquí no se destacan otra vez
        var conPortada = categoria is null && pagina <= 1;
        var modelo = await BoletinAsync(null, categoria, pagina, destacarEventos: !conPortada);
        foreach (var p in modelo.Publicaciones.Concat(modelo.ProximosEventos)) p.MostrarFundacion = true;
        if (conPortada) modelo.Periodico = await _periodico.ArmarAsync();
        return View(modelo);
    }

    [HttpGet("fundaciones/{slug}/boletin")]
    public async Task<IActionResult> DeFundacion(string slug, CategoriaPublicacion? categoria, int pagina = 1)
    {
        var esal = await _db.Esales.AsNoTracking().FirstOrDefaultAsync(e => e.Slug == slug && e.Activa);
        if (esal is null) return NoDisponible();

        var modelo = await BoletinAsync(esal.Id, categoria, pagina);
        modelo.Slug = esal.Slug;
        modelo.NombreEsal = esal.Nombre;
        modelo.LogoUrl = esal.LogoRuta is null ? null : _archivos.UrlPublica(esal.LogoRuta);
        return View(modelo);
    }

    [HttpGet("fundaciones/{slug}/boletin/{id:int}")]
    public async Task<IActionResult> Publicacion(string slug, int id)
    {
        var p = await Visibles().Include(x => x.Esal).Include(x => x.Causa)
            .FirstOrDefaultAsync(x => x.Id == id && x.Esal!.Slug == slug);
        if (p is null) return NoDisponible();

        var otras = await Visibles().Include(x => x.Esal)
            .Where(x => x.EsalId == p.EsalId && x.Id != id)
            .OrderByDescending(x => x.FechaPublicacion).Take(3).ToListAsync();

        var tarjeta = Tarjeta(p);
        return View(new PublicacionDetalleViewModel
        {
            Publicacion = tarjeta,
            Contenido = p.Contenido,
            LogoUrl = p.Esal!.LogoRuta is null ? null : _archivos.UrlPublica(p.Esal.LogoRuta),
            CausaId = p.Causa is not null && p.Causa.EsalId == p.EsalId ? p.CausaId : null,
            TituloCausa = p.Causa?.Titulo,
            Otras = otras.Select(Tarjeta).ToList(),
            UrlCalendario = p.Categoria == CategoriaPublicacion.Evento && !tarjeta.EventoPasado ? UrlGoogleCalendar(p) : null
        });
    }

    // ---------- Apoyo ----------

    /// <summary>Publicaciones publicadas de fundaciones activas con página pública.</summary>
    private IQueryable<Publicacion> Visibles()
        => _db.Publicaciones.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.Estado == EstadoPublicacion.Publicada && p.Esal!.Activa && p.Esal.Slug != null);

    private async Task<BoletinPublicoViewModel> BoletinAsync(int? esalId, CategoriaPublicacion? categoria, int pagina, bool destacarEventos = true)
    {
        var visibles = Visibles();
        if (esalId is int id) visibles = visibles.Where(p => p.EsalId == id);
        if (categoria is CategoriaPublicacion c && !Enum.IsDefined(c)) categoria = null;
        pagina = Math.Max(1, pagina);

        // Escenario 2: próximos eventos, el más cercano primero (solo en la primera página y sin filtro, o filtrando eventos)
        var ahora = DateTime.UtcNow;
        var proximos = destacarEventos && pagina == 1 && (categoria is null || categoria == CategoriaPublicacion.Evento)
            ? await visibles.Include(p => p.Esal)
                .Where(p => p.Categoria == CategoriaPublicacion.Evento && p.FechaEvento >= ahora)
                .OrderBy(p => p.FechaEvento).Take(6).ToListAsync()
            : new List<Publicacion>();

        var consulta = visibles;
        if (categoria is CategoriaPublicacion cat) consulta = consulta.Where(p => p.Categoria == cat);
        // Los eventos que ya se destacaron arriba no se repiten en la lista
        var idsProximos = proximos.Select(p => p.Id).ToList();
        consulta = consulta.Where(p => !idsProximos.Contains(p.Id));

        var lista = await consulta.Include(p => p.Esal)
            .OrderByDescending(p => p.FechaPublicacion).ThenByDescending(p => p.Id)
            .Skip((pagina - 1) * PorPagina).Take(PorPagina + 1)
            .ToListAsync();

        return new BoletinPublicoViewModel
        {
            Categoria = categoria,
            Pagina = pagina,
            HayMas = lista.Count > PorPagina,
            ProximosEventos = proximos.Select(Tarjeta).ToList(),
            Publicaciones = lista.Take(PorPagina).Select(Tarjeta).ToList()
        };
    }

    private PublicacionTarjeta Tarjeta(Publicacion p) => TarjetasBoletin.Desde(p, _archivos);

    /// <summary>Plantilla de Google Calendar con el evento (2 horas por defecto, porque no se guarda la hora de fin).</summary>
    private string UrlGoogleCalendar(Publicacion p)
    {
        var inicio = p.FechaEvento!.Value;
        var fin = inicio.AddHours(2);
        var detalle = $"{TarjetasBoletin.Recortar(p.Contenido, 500)}\n\n{Request.Scheme}://{Request.Host}/fundaciones/{p.Esal!.Slug}/boletin/{p.Id}";
        return "https://calendar.google.com/calendar/render?action=TEMPLATE"
            + "&text=" + Uri.EscapeDataString(p.Titulo)
            + "&dates=" + inicio.ToString("yyyyMMdd'T'HHmmss'Z'") + "/" + fin.ToString("yyyyMMdd'T'HHmmss'Z'")
            + "&location=" + Uri.EscapeDataString(p.LugarEvento ?? "")
            + "&details=" + Uri.EscapeDataString(detalle);
    }

    private IActionResult NoDisponible()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("NoDisponible");
    }
}
