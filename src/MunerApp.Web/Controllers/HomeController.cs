using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Models.Publico;

namespace MunerApp.Web.Controllers;

public class HomeController : Controller
{
    private readonly IWebHostEnvironment _entorno;
    private readonly MunerAppDbContext _db;
    private readonly IAlmacenamientoArchivos _archivos;

    public HomeController(IWebHostEnvironment entorno, MunerAppDbContext db, IAlmacenamientoArchivos archivos)
    {
        _entorno = entorno;
        _db = db;
        _archivos = archivos;
    }

    public async Task<IActionResult> Index()
    {
        // Las fundaciones activas más recientes, para la sección "Fundaciones" del inicio
        var esales = await _db.Esales.AsNoTracking()
            .Where(e => e.Activa && e.Slug != null)
            .OrderByDescending(e => e.FechaActualizacionPerfil ?? e.FechaRegistro)
            .Take(3)
            .Select(e => new { e.Slug, e.Nombre, e.TipoEntidad, e.DescripcionCorta, e.Ciudad, e.LogoRuta })
            .ToListAsync();

        return View(esales.Select(e => new FundacionTarjeta
        {
            Slug = e.Slug!,
            Nombre = e.Nombre,
            TipoEntidad = e.TipoEntidad,
            DescripcionCorta = e.DescripcionCorta,
            Ciudad = e.Ciudad,
            LogoUrl = e.LogoRuta is null ? null : _archivos.UrlPublica(e.LogoRuta)
        }).ToList());
    }

    /// <summary>Guía de estilos para el equipo. Solo disponible en desarrollo.</summary>
    public IActionResult Estilos() => _entorno.IsDevelopment() ? View() : NotFound();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();
}
