using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Constantes;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Models.Publico;
using MunerApp.Web.Servicios;

namespace MunerApp.Web.Controllers;

/// <summary>
/// Páginas públicas de las fundaciones: directorio (HU-010), perfil (HU-009, HU-011, HU-012, HU-016)
/// y opción "Donar" (HU-013). Solo se muestran fundaciones activas (HU-006, escenario 3).
/// Las consultas usan IgnoreQueryFilters y filtran explícitamente por la fundación consultada:
/// así un administrador de otra fundación también ve el perfil completo.
/// </summary>
[Route("fundaciones")]
public class FundacionesController : Controller
{
    private const int PorPagina = 9;

    private readonly MunerAppDbContext _db;
    private readonly IAlmacenamientoArchivos _archivos;
    private readonly IModuloService _modulos;
    private readonly CausasPublicas _causas;

    public FundacionesController(MunerAppDbContext db, IAlmacenamientoArchivos archivos, IModuloService modulos, CausasPublicas causas)
    {
        _db = db;
        _archivos = archivos;
        _modulos = modulos;
        _causas = causas;
    }

    // ---------- HU-010: listado y búsqueda ----------

    [HttpGet("")]
    public async Task<IActionResult> Index(string? q, int pagina = 1)
    {
        var consulta = _db.Esales.AsNoTracking().Where(ReglasPublicacion.EnDirectorio);

        q = q?.Trim();
        if (!string.IsNullOrEmpty(q))
            consulta = consulta.Where(e => e.Nombre.Contains(q) || (e.Ciudad != null && e.Ciudad.Contains(q)));

        var total = await consulta.CountAsync();
        var totalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)PorPagina));
        pagina = Math.Clamp(pagina, 1, totalPaginas);

        var esales = await consulta
            .OrderBy(e => e.Nombre)
            .Skip((pagina - 1) * PorPagina)
            .Take(PorPagina)
            .Select(e => new { e.Slug, e.Nombre, e.TipoEntidad, e.DescripcionCorta, e.Ciudad, e.LogoRuta })
            .ToListAsync();

        return View(new DirectorioViewModel
        {
            Busqueda = q,
            Pagina = pagina,
            TotalPaginas = totalPaginas,
            Total = total,
            Fundaciones = esales.Select(e => new FundacionTarjeta
            {
                Slug = e.Slug!,
                Nombre = e.Nombre,
                TipoEntidad = e.TipoEntidad,
                DescripcionCorta = e.DescripcionCorta,
                Ciudad = e.Ciudad,
                LogoUrl = UrlArchivo(e.LogoRuta)
            }).ToList()
        });
    }

    // ---------- HU-010 escenario 2: perfil público ----------

    [HttpGet("{slug}")]
    public async Task<IActionResult> Perfil(string slug)
    {
        var esal = await BuscarActivaAsync(slug);
        if (esal is null) return NoDisponible();

        var redes = await _db.RedesSociales.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.EsalId == esal.Id)
            .OrderBy(r => r.Tipo)
            .ToListAsync();

        var fotos = await _db.FotosEsal.IgnoreQueryFilters().AsNoTracking()
            .Where(f => f.EsalId == esal.Id)
            .OrderBy(f => f.Orden).ThenBy(f => f.Id)
            .Select(f => f.Ruta)
            .ToListAsync();

        // HU-012: solo los documentos visibles
        var documentos = await _db.DocumentosTransparencia.IgnoreQueryFilters().AsNoTracking()
            .Where(d => d.EsalId == esal.Id && d.Visible)
            .OrderByDescending(d => d.FechaPublicacion)
            .ToListAsync();

        var tieneDatos = await _db.DatosDonacion.IgnoreQueryFilters().AnyAsync(d => d.EsalId == esal.Id);
        var activos = (await _modulos.ObtenerActivosAsync(esal.Id)).Select(m => m.Codigo).ToHashSet();
        var hayApadrinables = activos.Contains(CodigosModulo.Beneficiarios)
            && await _db.Beneficiarios.IgnoreQueryFilters().AnyAsync(b => b.EsalId == esal.Id && b.Apadrinable);

        var modelo = new PerfilPublicoViewModel
        {
            Id = esal.Id,
            Slug = esal.Slug!,
            Nombre = esal.Nombre,
            TipoEntidad = esal.TipoEntidad,
            Nit = esal.Nit,
            DescripcionCorta = esal.DescripcionCorta,
            Historia = esal.Historia,
            Mision = esal.Mision,
            Vision = esal.Vision,
            Ciudad = esal.Ciudad,
            Telefono = esal.Telefono,
            Correo = esal.CorreoContacto,
            LogoUrl = UrlArchivo(esal.LogoRuta),
            FechaRegistro = esal.FechaRegistro,
            Fotos = fotos.Select(f => _archivos.UrlPublica(f)).ToList(),
            Redes = redes.Select(r => new RedPublica(r.Tipo, r.Url)).ToList(),
            Documentos = documentos.Select(d => new DocumentoPublico(
                d.Titulo, Textos.De(d.Categoria), d.Descripcion, _archivos.UrlPublica(d.Ruta),
                d.Extension, d.FechaPublicacion, d.TamanoBytes)).ToList(),
            TieneDatosDonacion = tieneDatos,
            TieneModulosApoyo = activos.Overlaps(new[] { CodigosModulo.Beneficiarios, CodigosModulo.Adopcion, CodigosModulo.Tienda }),
            FormasAyuda = ConstruirFormasAyuda(esal, activos, hayApadrinables),
            Causas = await _causas.ListarAsync(esal.Id)
        };

        return View(modelo);
    }

    // ---------- HU-013: opción "Donar" ----------

    [HttpGet("{slug}/donar")]
    public async Task<IActionResult> Donar(string slug, int? causa)
    {
        var esal = await BuscarActivaAsync(slug);
        if (esal is null) return NoDisponible();

        // Donación para una causa: solo si la causa sigue recibiendo donaciones
        var causaAbierta = causa is int causaId ? await _causas.ObtenerAsync(esal.Id, causaId) : null;
        if (causaAbierta is { RecibeDonaciones: false }) causaAbierta = null;

        var datos = await _db.DatosDonacion.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(d => d.EsalId == esal.Id);

        return View(new DonarViewModel
        {
            Slug = esal.Slug!,
            Nombre = esal.Nombre,
            LogoUrl = UrlArchivo(esal.LogoRuta),
            Disponible = datos is not null,
            Titular = datos?.Titular,
            DocumentoTitular = datos?.DocumentoTitular,
            Entidad = datos?.Entidad,
            TipoCuenta = datos is null ? null : Textos.De(datos.TipoCuenta, datos.TipoLlave),
            EsLlave = datos?.TipoCuenta == TipoCuentaDonacion.Llave,
            Numero = datos?.Numero,
            Instrucciones = datos?.Instrucciones,
            PagosEnLineaActivos = await _db.ConfigPasarelas.IgnoreQueryFilters().AnyAsync(c => c.EsalId == esal.Id && c.Activa),
            CausaId = causaAbierta?.Id,
            NombreCausa = causaAbierta?.Titulo
        });
    }

    // ---------- HU-020: apadrinamiento (solo se muestra la información pública) ----------

    [HttpGet("{slug}/apadrinar")]
    public async Task<IActionResult> Apadrinar(string slug)
    {
        var esal = await BuscarActivaAsync(slug);
        if (esal is null || !await _modulos.EstaActivoAsync(esal.Id, CodigosModulo.Beneficiarios)) return NoDisponible();

        // Se leen solo los campos públicos: nunca la hoja de vida interna ni la historia clínica
        var lista = await _db.Beneficiarios.IgnoreQueryFilters().AsNoTracking()
            .Where(b => b.EsalId == esal.Id && b.Apadrinable)
            .OrderBy(b => b.Nombre)
            .Select(b => new { b.Id, b.Nombre, b.FechaNacimiento, b.Sexo, b.Color, b.FotoPublicaRuta, b.HistoriaPublica, b.AporteSugerido })
            .ToListAsync();

        return View(new ApadrinablesViewModel
        {
            Slug = esal.Slug!,
            Nombre = esal.Nombre,
            LogoUrl = UrlArchivo(esal.LogoRuta),
            Beneficiarios = lista.Select(b => TarjetaApadrinable(b.Id, b.Nombre, b.FechaNacimiento, b.Sexo, b.Color, b.FotoPublicaRuta, b.HistoriaPublica, b.AporteSugerido)).ToList()
        });
    }

    [HttpGet("{slug}/apadrinar/{id:int}")]
    public async Task<IActionResult> FichaApadrinable(string slug, int id)
    {
        var esal = await BuscarActivaAsync(slug);
        if (esal is null || !await _modulos.EstaActivoAsync(esal.Id, CodigosModulo.Beneficiarios)) return NoDisponible();

        var b = await _db.Beneficiarios.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Id == id && x.EsalId == esal.Id && x.Apadrinable)
            .Select(x => new { x.Id, x.Nombre, x.FechaNacimiento, x.Sexo, x.Color, x.FotoPublicaRuta, x.HistoriaPublica, x.AporteSugerido })
            .FirstOrDefaultAsync();
        if (b is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View("ApadrinableNoDisponible", new ApadrinablesViewModel { Slug = esal.Slug!, Nombre = esal.Nombre, LogoUrl = UrlArchivo(esal.LogoRuta) });
        }

        int? propio = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var usuarioId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            propio = await _db.Apadrinamientos.IgnoreQueryFilters().AsNoTracking()
                .Where(a => a.PadrinoId == usuarioId && a.BeneficiarioId == id && a.Estado == EstadoApadrinamiento.Activo)
                .Select(a => (int?)a.Id)
                .FirstOrDefaultAsync();
        }

        return View(new ApadrinableFichaViewModel
        {
            ApadrinamientoPropioId = propio,
            Slug = esal.Slug!,
            NombreEsal = esal.Nombre,
            LogoUrl = UrlArchivo(esal.LogoRuta),
            Beneficiario = TarjetaApadrinable(b.Id, b.Nombre, b.FechaNacimiento, b.Sexo, b.Color, b.FotoPublicaRuta, b.HistoriaPublica, b.AporteSugerido)
        });
    }

    private ApadrinableTarjeta TarjetaApadrinable(int id, string nombre, DateTime nacimiento, SexoBeneficiario sexo, string color,
        string? fotoRuta, string? historia, decimal? aporte) => new()
    {
        Id = id,
        Nombre = nombre,
        Edad = Servicios.Formatos.Edad(nacimiento),
        Sexo = Textos.De(sexo),
        Color = color,
        FotoUrl = UrlArchivo(fotoRuta),
        Historia = historia ?? "",
        AporteSugerido = aporte ?? 0
    };

    /// <summary>HU-016: las opciones dependen de los módulos activos de la fundación.</summary>
    private List<FormaAyuda> ConstruirFormasAyuda(Esal esal, ISet<string> activos, bool hayApadrinables)
    {
        var slug = esal.Slug!;
        var formas = new List<FormaAyuda>
        {
            new(CodigosModulo.Donaciones, "Donar",
                "Transfiere a la cuenta oficial de la fundación y reporta tu donación para recibir tu comprobante.",
                "bi-cash-coin", "acento", $"/fundaciones/{slug}/donar", false, true)
        };

        if (!esal.VoluntariadoPausado)
            formas.Add(new(CodigosModulo.Voluntariado, "Ser voluntario",
                "Apoya con tu tiempo o tus conocimientos en las labores que la fundación necesita.",
                "bi-people", "exito", $"/fundaciones/{slug}/voluntariado", true, true));

        // Módulos configurables: se habilitan en los sprints 3 y 4
        if (activos.Contains(CodigosModulo.Beneficiarios))
            formas.Add(new(CodigosModulo.Beneficiarios, "Apadrinar",
                "Acompaña a un beneficiario con un aporte periódico y sigue sus novedades.",
                "bi-balloon-heart", "", $"/fundaciones/{slug}/apadrinar", false, hayApadrinables));

        if (activos.Contains(CodigosModulo.Adopcion))
            formas.Add(new(CodigosModulo.Adopcion, "Adoptar",
                "Dale un hogar a uno de los peludos que la fundación tiene en adopción.",
                "bi-house-heart", "acento", null, true, false));

        if (activos.Contains(CodigosModulo.Tienda))
            formas.Add(new(CodigosModulo.Tienda, "Comprar en su tienda",
                "Productos de la fundación: cada compra también ayuda.",
                "bi-bag-heart", "alerta", null, false, false));

        return formas;
    }

    private Task<Esal?> BuscarActivaAsync(string slug)
        => _db.Esales.AsNoTracking().FirstOrDefaultAsync(e => e.Slug == slug && e.Activa);

    private IActionResult NoDisponible()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("NoDisponible");
    }

    private string? UrlArchivo(string? clave) => string.IsNullOrEmpty(clave) ? null : _archivos.UrlPublica(clave);
}
