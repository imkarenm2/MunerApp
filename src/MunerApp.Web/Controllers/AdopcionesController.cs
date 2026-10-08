using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Constantes;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Models.Publico;

namespace MunerApp.Web.Controllers;

/// <summary>
/// Solicitud de adopción en línea. HU-029: la persona lee y acepta las recomendaciones
/// y responsabilidades; solo entonces se habilita el formulario (HU-030 a HU-032).
/// </summary>
[Authorize]
public class AdopcionesController : Controller
{
    private readonly MunerAppDbContext _db;
    private readonly IAlmacenamientoArchivos _archivos;
    private readonly IModuloService _modulos;
    private readonly IEsalActual _esalActual;

    public AdopcionesController(MunerAppDbContext db, IAlmacenamientoArchivos archivos, IModuloService modulos, IEsalActual esalActual)
    {
        _db = db;
        _archivos = archivos;
        _modulos = modulos;
        _esalActual = esalActual;
    }

    private string UsuarioId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // ---------- HU-029: recomendaciones y responsabilidades ----------

    /// <summary>Cualquier visitante puede leer las recomendaciones; para aceptarlas debe iniciar sesión (escenario 3).</summary>
    [AllowAnonymous]
    [HttpGet("fundaciones/{slug}/adoptar")]
    public async Task<IActionResult> Recomendaciones(string slug)
    {
        var esal = await BuscarConAdopcionAsync(slug);
        if (esal is null) return ModuloNoDisponible();
        return View(await PrepararAsync(new RecomendacionesAdopcionViewModel(), esal));
    }

    [HttpPost("fundaciones/{slug}/adoptar")]
    public async Task<IActionResult> Recomendaciones(string slug, RecomendacionesAdopcionViewModel model)
    {
        var esal = await BuscarConAdopcionAsync(slug);
        if (esal is null) return ModuloNoDisponible();

        var acepto = model.Acepto;
        model = await PrepararAsync(model, esal);
        if (model.Aviso is not null) return View(model);

        // Escenario 2: sin aceptar no se accede al formulario
        if (!acepto)
        {
            ModelState.AddModelError(nameof(model.Acepto), "Para continuar debes leer y aceptar las recomendaciones y responsabilidades.");
            return View(model);
        }

        // Escenario 1: la aceptación queda registrada y habilita el formulario
        var borrador = await BuscarBorradorAsync(esal.Id);
        if (borrador is null)
        {
            borrador = new SolicitudAdopcion { EsalId = esal.Id, UsuarioId = UsuarioId };
            _db.SolicitudesAdopcion.Add(borrador);
        }
        borrador.FechaAceptacionRecomendaciones = DateTime.UtcNow;
        borrador.FechaActualizacion = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Formulario), new { slug });
    }

    // ---------- HU-030 a HU-032: formulario por secciones ----------

    [HttpGet("fundaciones/{slug}/adoptar/formulario")]
    public async Task<IActionResult> Formulario(string slug)
    {
        var esal = await BuscarConAdopcionAsync(slug);
        if (esal is null) return ModuloNoDisponible();

        // Escenario 2 de HU-029: no se entra al formulario por URL sin haber aceptado
        // (el borrador solo se crea al aceptar las recomendaciones)
        var borrador = await BuscarBorradorAsync(esal.Id);
        if (borrador is null)
        {
            TempData["Error"] = "Antes de diligenciar el formulario debes leer y aceptar las recomendaciones.";
            return RedirectToAction(nameof(Recomendaciones), new { slug });
        }

        return View(new FormularioAdopcionViewModel
        {
            Slug = esal.Slug!,
            NombreEsal = esal.Nombre,
            LogoUrl = UrlArchivo(esal.LogoRuta)
        });
    }

    /// <summary>Fundación activa con el módulo de adopción activo, o null.</summary>
    private async Task<Esal?> BuscarConAdopcionAsync(string slug)
    {
        var esal = await _db.Esales.AsNoTracking().FirstOrDefaultAsync(e => e.Slug == slug && e.Activa);
        return esal is not null && await _modulos.EstaActivoAsync(esal.Id, CodigosModulo.Adopcion) ? esal : null;
    }

    private Task<SolicitudAdopcion?> BuscarBorradorAsync(int esalId)
        => _db.SolicitudesAdopcion.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.EsalId == esalId && s.UsuarioId == UsuarioId && s.Estado == EstadoSolicitudAdopcion.Borrador);

    private async Task<RecomendacionesAdopcionViewModel> PrepararAsync(RecomendacionesAdopcionViewModel model, Esal esal)
    {
        var config = await _db.ConfigAdopciones.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(c => c.EsalId == esal.Id);

        model.Slug = esal.Slug!;
        model.NombreEsal = esal.Nombre;
        model.LogoUrl = UrlArchivo(esal.LogoRuta);
        model.Recomendaciones = ConfigAdopcion.ComoLista(config?.Recomendaciones);
        model.Acepto = false;

        if (User.Identity?.IsAuthenticated != true) return model;

        if (_esalActual.EsalId == esal.Id)
            model.Aviso = $"Haces parte del equipo de {esal.Nombre}: las solicitudes de adopción las hacen las personas interesadas desde su cuenta personal.";
        else if (_esalActual.EsalId is not null || _esalActual.EsSuperAdmin)
            model.Aviso = "Las cuentas de una fundación no pueden solicitar adopciones. Si quieres adoptar a título personal, crea una cuenta con tu correo personal.";
        else if (await _db.SolicitudesAdopcion.IgnoreQueryFilters()
                     .AnyAsync(s => s.EsalId == esal.Id && s.UsuarioId == UsuarioId && SolicitudAdopcion.EstadosEnProceso.Contains(s.Estado)))
            model.Aviso = $"Ya tienes una solicitud de adopción en proceso con {esal.Nombre}. Te avisaremos cuando la revisen.";
        else
            model.TieneBorrador = await BuscarBorradorAsync(esal.Id) is not null;

        return model;
    }

    private IActionResult ModuloNoDisponible()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("ModuloNoDisponible");
    }

    private string? UrlArchivo(string? clave) => string.IsNullOrEmpty(clave) ? null : _archivos.UrlPublica(clave);
}
