using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Application.Seguridad;
using MunerApp.Domain.Constantes;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Models.Publico;
using MunerApp.Web.Servicios;

namespace MunerApp.Web.Controllers;

public class HomeController : Controller
{
    private readonly IWebHostEnvironment _entorno;
    private readonly MunerAppDbContext _db;
    private readonly IAlmacenamientoArchivos _archivos;
    private readonly CausasPublicas _causas;

    public HomeController(IWebHostEnvironment entorno, MunerAppDbContext db, IAlmacenamientoArchivos archivos, CausasPublicas causas)
    {
        _entorno = entorno;
        _db = db;
        _archivos = archivos;
        _causas = causas;
    }

    public async Task<IActionResult> Index()
    {
        // Estando autenticados, el superadmin y el equipo de una fundación van a su panel
        if (User.IsInRole(Roles.SuperAdministrador))
            return RedirectToAction("Index", "Resumen", new { area = "Plataforma" });
        if (User.IsInRole(Roles.AdministradorESAL) || User.IsInRole(Roles.Voluntario))
            return RedirectToAction("Index", "Panel", new { area = "Fundacion" });

        // Todo lo público se consulta sin el filtro por fundación y solo de fundaciones activas y publicadas
        var esalesPublicas = _db.Esales.AsNoTracking().Where(ReglasPublicacion.EnDirectorio);
        var idsPublicas = await esalesPublicas.Select(e => e.Id).ToListAsync();
        var enCasa = ReglasBeneficiario.EnCasa;
        var hogares = new[] { EstadoBeneficiario.Adoptado, EstadoBeneficiario.EncontroSuHogar };
        var beneficiarios = _db.Beneficiarios.IgnoreQueryFilters().AsNoTracking().Where(b => idsPublicas.Contains(b.EsalId));

        var esales = await esalesPublicas
            .OrderByDescending(e => e.FechaActualizacionPerfil ?? e.FechaRegistro)
            .Select(e => new { e.Slug, e.Nombre, e.TipoEntidad, e.DescripcionCorta, e.Ciudad, e.LogoRuta })
            .ToListAsync();

        var causas = await _causas.ListarAsync(esalId: null, maxCerradas: 0);

        var gatos = await beneficiarios
            .Where(b => b.Apadrinable && enCasa.Contains(b.Estado) && b.FotoPublicaRuta != null)
            .OrderByDescending(b => b.FechaApadrinable)
            .Take(4)
            .Select(b => new { b.Id, b.Nombre, b.FotoPublicaRuta, b.HistoriaPublica, Fundacion = b.Esal!.Nombre, b.Esal.Slug })
            .ToListAsync();

        var fotos = await _db.FotosEsal.IgnoreQueryFilters().AsNoTracking()
            .Where(f => idsPublicas.Contains(f.EsalId))
            .OrderBy(f => f.Orden).ThenByDescending(f => f.Id)
            .Select(f => f.Ruta).Take(3).ToListAsync();

        var modelo = new HomeViewModel
        {
            Fundaciones = esales.Take(3).Select(e => new FundacionTarjeta
            {
                Slug = e.Slug!,
                Nombre = e.Nombre,
                TipoEntidad = e.TipoEntidad,
                DescripcionCorta = e.DescripcionCorta,
                Ciudad = e.Ciudad,
                LogoUrl = e.LogoRuta is null ? null : _archivos.UrlPublica(e.LogoRuta)
            }).ToList(),
            Causas = causas.Abiertas.Where(c => c.RecibeDonaciones).Take(3).ToList(),
            Gatos = gatos.Select(g => new GatoApadrinableHome(g.Id, g.Nombre,
                g.FotoPublicaRuta is null ? null : _archivos.UrlPublica(g.FotoPublicaRuta), g.Fundacion, g.Slug!, g.HistoriaPublica)).ToList(),
            FotosHero = fotos.Select(_archivos.UrlPublica).ToList(),
            FundacionesPublicadas = esales.Count,
            GatosConHogar = await beneficiarios.CountAsync(b => hogares.Contains(b.Estado)),
            GatosCuidados = await beneficiarios.CountAsync(b => enCasa.Contains(b.Estado)),
            Donado = await _db.Donaciones.IgnoreQueryFilters()
                .Where(d => d.Estado == EstadoDonacion.Confirmada && idsPublicas.Contains(d.EsalId))
                .SumAsync(d => (decimal?)d.Valor) ?? 0,
            CausasAbiertas = causas.Abiertas.Count(c => c.RecibeDonaciones),
            Padrinos = await _db.Apadrinamientos.IgnoreQueryFilters()
                .Where(a => a.Estado == EstadoApadrinamiento.Activo && idsPublicas.Contains(a.EsalId))
                .Select(a => a.PadrinoId).Distinct().CountAsync(),
            Municipios = Region.Municipios
                .Select(m => new MunicipioHome(m, esales.Count(e => MismoMunicipio(e.Ciudad, m))))
                .ToList()
        };

        // El donante que ya entró ve su resumen arriba
        if (User.Identity?.IsAuthenticated == true)
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var suyas = _db.Donaciones.IgnoreQueryFilters().AsNoTracking().Where(d => d.DonanteId == id);
            modelo.Donante = new ResumenDonanteHome(
                User.FindFirst(MunerAppClaims.NombreCompleto)?.Value?.Split(' ')[0] ?? "",
                await suyas.CountAsync(d => d.Estado == EstadoDonacion.Confirmada),
                await suyas.Where(d => d.Estado == EstadoDonacion.Confirmada).SumAsync(d => (decimal?)d.Valor) ?? 0,
                await suyas.CountAsync(d => d.Estado == EstadoDonacion.Pendiente),
                await _db.Apadrinamientos.IgnoreQueryFilters().CountAsync(a => a.PadrinoId == id && a.Estado == EstadoApadrinamiento.Activo));
        }

        return View(modelo);
    }

    /// <summary>Compara la ciudad escrita por la fundación con el municipio, sin tildes ni mayúsculas.</summary>
    private static bool MismoMunicipio(string? ciudad, string municipio)
        => !string.IsNullOrWhiteSpace(ciudad) && SinTildes(ciudad).Contains(SinTildes(municipio));

    private static string SinTildes(string texto)
    {
        var normal = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normal.Length);
        foreach (var c in normal)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().ToLowerInvariant();
    }

    /// <summary>Guía de estilos para el equipo. Solo disponible en desarrollo.</summary>
    public IActionResult Estilos() => _entorno.IsDevelopment() ? View() : NotFound();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();
}
