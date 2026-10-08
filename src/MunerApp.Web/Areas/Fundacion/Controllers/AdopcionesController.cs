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
using MunerApp.Web.Servicios;
using MunerApp.Web.Validacion;

namespace MunerApp.Web.Areas.Fundacion.Controllers;

/// <summary>
/// Módulo de adopción de la fundación. Configuración del proceso: recomendaciones que la persona
/// acepta antes del formulario (HU-029), valor del aporte e información de toxoplasmosis (HU-032).
/// </summary>
[Area("Fundacion")]
[Authorize(Roles = Roles.AdministradorESAL)]
[RequiereModulo(CodigosModulo.Adopcion)]
public class AdopcionesController : Controller
{
    private const decimal AporteMaximo = 10_000_000;

    private readonly MunerAppDbContext _db;
    private readonly IEsalActual _esalActual;
    private readonly IAlmacenamientoArchivos _archivos;

    public AdopcionesController(MunerAppDbContext db, IEsalActual esalActual, IAlmacenamientoArchivos archivos)
    {
        _db = db;
        _esalActual = esalActual;
        _archivos = archivos;
    }

    private int EsalId => _esalActual.EsalId ?? throw new InvalidOperationException("El usuario no pertenece a una ESAL.");

    [HttpGet]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> Configuracion()
    {
        var config = await _db.ConfigAdopciones.AsNoTracking().FirstOrDefaultAsync();
        var model = new ConfigAdopcionViewModel
        {
            Recomendaciones = config?.Recomendaciones ?? ConfigAdopcion.RecomendacionesPredeterminadas,
            ValorAporte = ((long)(config?.ValorAporte ?? ConfigAdopcion.ValorAportePredeterminado)).ToString("N0", new System.Globalization.CultureInfo("es-CO")),
            MensajeToxoplasmosis = config?.MensajeToxoplasmosis ?? ConfigAdopcion.MensajeToxoplasmosisPredeterminado
        };
        return View(await PrepararAsync(model, config));
    }

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    [RequestSizeLimit(ValidadorArchivos.LimitePeticionBytes)]
    public async Task<IActionResult> Configuracion(ConfigAdopcionViewModel model)
    {
        var config = await _db.ConfigAdopciones.FirstOrDefaultAsync();

        // HU-029: una recomendación por línea, sin líneas vacías
        var lineas = ConfigAdopcion.ComoLista(model.Recomendaciones ?? "");
        if (string.IsNullOrWhiteSpace(model.Recomendaciones) || lineas.Count == 0)
            ModelState.AddModelError(nameof(model.Recomendaciones), "Escribe al menos una recomendación.");

        // HU-032: valor del aporte y la imagen de toxoplasmosis
        var aporte = Formatos.LeerPesos(model.ValorAporte);
        if (!string.IsNullOrWhiteSpace(model.ValorAporte))
        {
            if (aporte is null)
                ModelState.AddModelError(nameof(model.ValorAporte), "Escribe el valor solo con números, por ejemplo 100.000.");
            else if (aporte > AporteMaximo)
                ModelState.AddModelError(nameof(model.ValorAporte), $"El valor no puede superar {Formatos.Pesos(AporteMaximo)}.");
        }

        ArchivoValidado? imagen = null;
        if (model.ImagenToxoplasmosis is { Length: > 0 })
        {
            imagen = await ValidadorArchivos.ValidarAsync(model.ImagenToxoplasmosis, TipoArchivo.Imagen);
            if (!imagen.Valido) ModelState.AddModelError(nameof(model.ImagenToxoplasmosis), imagen.Error!);
        }

        if (!ModelState.IsValid) return View(await PrepararAsync(model, config));

        if (config is null)
        {
            config = new ConfigAdopcion { EsalId = EsalId };
            _db.ConfigAdopciones.Add(config);
        }

        if (config.ImagenToxoplasmosisRuta is not null && (imagen is not null || model.QuitarImagen))
        {
            await _archivos.EliminarAsync(config.ImagenToxoplasmosisRuta);
            config.ImagenToxoplasmosisRuta = null;
        }
        if (imagen is not null)
        {
            await using var stream = model.ImagenToxoplasmosis!.OpenReadStream();
            config.ImagenToxoplasmosisRuta = await _archivos.GuardarAsync(stream, $"esal/{EsalId}/adopcion", imagen.Extension, publico: true);
        }

        config.Recomendaciones = string.Join("\n", lineas);
        config.ValorAporte = aporte!.Value;
        config.MensajeToxoplasmosis = string.IsNullOrWhiteSpace(model.MensajeToxoplasmosis) ? null : model.MensajeToxoplasmosis.Trim();
        config.FechaActualizacion = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = "Guardaste la configuración de adopción. Las personas la verán al solicitar una adopción.";
        return RedirectToAction(nameof(Configuracion));
    }

    private async Task<ConfigAdopcionViewModel> PrepararAsync(ConfigAdopcionViewModel model, ConfigAdopcion? config)
    {
        model.Personalizadas = config is not null;
        model.FechaActualizacion = config?.FechaActualizacion;
        model.ImagenToxoplasmosisUrl = config?.ImagenToxoplasmosisRuta is null ? null : _archivos.UrlPublica(config.ImagenToxoplasmosisRuta);
        model.Slug = await _db.Esales.Where(e => e.Id == EsalId).Select(e => e.Slug).FirstOrDefaultAsync();
        return model;
    }
}
