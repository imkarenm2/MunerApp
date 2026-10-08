using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Constantes;
using MunerApp.Domain.Entities;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Areas.Fundacion.Models;
using MunerApp.Web.Filtros;
using MunerApp.Web.Seguridad;

namespace MunerApp.Web.Areas.Fundacion.Controllers;

/// <summary>
/// Módulo de adopción de la fundación. HU-029: recomendaciones y responsabilidades
/// que la persona interesada lee y acepta antes del formulario.
/// </summary>
[Area("Fundacion")]
[Authorize(Roles = Roles.AdministradorESAL)]
[RequiereModulo(CodigosModulo.Adopcion)]
public class AdopcionesController : Controller
{
    private readonly MunerAppDbContext _db;
    private readonly IEsalActual _esalActual;

    public AdopcionesController(MunerAppDbContext db, IEsalActual esalActual)
    {
        _db = db;
        _esalActual = esalActual;
    }

    private int EsalId => _esalActual.EsalId ?? throw new InvalidOperationException("El usuario no pertenece a una ESAL.");

    [HttpGet]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> Recomendaciones()
    {
        var config = await _db.ConfigAdopciones.AsNoTracking().FirstOrDefaultAsync();
        return View(new ConfigRecomendacionesViewModel
        {
            Recomendaciones = config?.Recomendaciones ?? ConfigAdopcion.RecomendacionesPredeterminadas,
            Personalizadas = config is not null,
            FechaActualizacion = config?.FechaActualizacion,
            Slug = await SlugAsync()
        });
    }

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> Recomendaciones(ConfigRecomendacionesViewModel model)
    {
        var config = await _db.ConfigAdopciones.FirstOrDefaultAsync();

        // Se guarda una recomendación por línea, sin líneas vacías
        var lineas = ConfigAdopcion.ComoLista(model.Recomendaciones ?? "");
        if (string.IsNullOrWhiteSpace(model.Recomendaciones) || lineas.Count == 0)
            ModelState.AddModelError(nameof(model.Recomendaciones), "Escribe al menos una recomendación.");

        if (!ModelState.IsValid)
        {
            model.Personalizadas = config is not null;
            model.FechaActualizacion = config?.FechaActualizacion;
            model.Slug = await SlugAsync();
            return View(model);
        }

        if (config is null)
        {
            config = new ConfigAdopcion { EsalId = EsalId };
            _db.ConfigAdopciones.Add(config);
        }
        config.Recomendaciones = string.Join("\n", lineas);
        config.FechaActualizacion = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = "Guardaste las recomendaciones. Las personas las leerán antes de solicitar una adopción.";
        return RedirectToAction(nameof(Recomendaciones));
    }

    private Task<string?> SlugAsync()
        => _db.Esales.Where(e => e.Id == EsalId).Select(e => e.Slug).FirstOrDefaultAsync();
}
