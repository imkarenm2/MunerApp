using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Areas.Fundacion.Models;
using MunerApp.Web.Seguridad;

namespace MunerApp.Web.Areas.Fundacion.Controllers;

/// <summary>
/// HU-045: llaves de Wompi de la fundación. El dinero llega a la cuenta Wompi de la ESAL;
/// MunerApp solo guarda las llaves (los secretos, cifrados) y nunca los vuelve a mostrar.
/// </summary>
[Area("Fundacion")]
[Authorize(Policy = Politicas.AdminEsalPrincipal)]
public class PasarelaController : Controller
{
    private readonly MunerAppDbContext _db;
    private readonly IEsalActual _esalActual;
    private readonly IWompiService _wompi;
    private readonly ISecretosService _secretos;

    public PasarelaController(MunerAppDbContext db, IEsalActual esalActual, IWompiService wompi, ISecretosService secretos)
    {
        _db = db;
        _esalActual = esalActual;
        _wompi = wompi;
        _secretos = secretos;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var config = await _db.ConfigPasarelas.AsNoTracking().FirstOrDefaultAsync();
        return View(new PasarelaViewModel
        {
            LlavePublica = config?.LlavePublica ?? string.Empty,
            TieneConfiguracion = config is not null,
            Activa = config?.Activa ?? false,
            Ambiente = config?.Ambiente.ToString()
        });
    }

    [HttpPost]
    public async Task<IActionResult> Index(PasarelaViewModel model)
    {
        var config = await _db.ConfigPasarelas.FirstOrDefaultAsync();
        model.TieneConfiguracion = config is not null;
        model.LlavePublica = model.LlavePublica?.Trim() ?? string.Empty;
        model.SecretoIntegridad = model.SecretoIntegridad?.Trim();
        model.SecretoEventos = model.SecretoEventos?.Trim();

        AmbientePasarela? ambiente = null;
        if (model.LlavePublica.StartsWith("pub_test_")) ambiente = AmbientePasarela.Sandbox;
        else if (model.LlavePublica.StartsWith("pub_prod_")) ambiente = AmbientePasarela.Produccion;

        if (ambiente is null)
            ModelState.AddModelError(nameof(model.LlavePublica), "La llave pública empieza por pub_test_ (pruebas) o pub_prod_ (producción).");

        var prefijo = ambiente == AmbientePasarela.Produccion ? "prod" : "test";
        ValidarSecreto(model.SecretoIntegridad, $"{prefijo}_integrity_", nameof(model.SecretoIntegridad), "integridad", obligatorio: config is null);
        ValidarSecreto(model.SecretoEventos, $"{prefijo}_events_", nameof(model.SecretoEventos), "eventos", obligatorio: config is null);

        if (!ModelState.IsValid) return View(model);

        // Escenario 1 y 2: se verifica la llave pública directamente con Wompi
        var validacion = await _wompi.ValidarLlavePublicaAsync(model.LlavePublica, ambiente!.Value);
        if (validacion == ResultadoValidacionLlave.Invalida)
        {
            ModelState.AddModelError(nameof(model.LlavePublica), "Wompi no reconoce esta llave pública. Revisa que la copiaste completa desde tu panel de Wompi.");
            return View(model);
        }
        if (validacion == ResultadoValidacionLlave.SinConexion)
        {
            ModelState.AddModelError(string.Empty, "No pudimos comunicarnos con Wompi para verificar la llave. Intenta de nuevo en unos minutos.");
            return View(model);
        }

        if (config is null)
        {
            config = new ConfigPasarela
            {
                EsalId = _esalActual.EsalId ?? throw new InvalidOperationException("El usuario no pertenece a una ESAL.")
            };
            _db.ConfigPasarelas.Add(config);
        }

        config.Proveedor = "WOMPI";
        config.LlavePublica = model.LlavePublica;
        config.Ambiente = ambiente.Value;
        if (!string.IsNullOrEmpty(model.SecretoIntegridad))
            config.SecretoIntegridadCifrado = _secretos.Proteger(model.SecretoIntegridad);
        if (!string.IsNullOrEmpty(model.SecretoEventos))
            config.SecretoEventosCifrado = _secretos.Proteger(model.SecretoEventos);
        config.Activa = true;

        await _db.SaveChangesAsync();
        TempData["Mensaje"] = ambiente == AmbientePasarela.Sandbox
            ? "Listo: los pagos en línea quedaron habilitados en modo de pruebas (sandbox)."
            : "Listo: los pagos en línea quedaron habilitados.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Desactivar()
    {
        var config = await _db.ConfigPasarelas.FirstOrDefaultAsync();
        if (config is not null)
        {
            config.Activa = false;
            await _db.SaveChangesAsync();
            TempData["Mensaje"] = "Desactivaste los pagos en línea. Los donantes verán los datos para donar por transferencia.";
        }
        return RedirectToAction(nameof(Index));
    }

    private void ValidarSecreto(string? valor, string prefijoEsperado, string campo, string nombre, bool obligatorio)
    {
        if (string.IsNullOrEmpty(valor))
        {
            if (obligatorio) ModelState.AddModelError(campo, $"Ingresa el secreto de {nombre}.");
            return;
        }
        if (!valor.StartsWith(prefijoEsperado))
            ModelState.AddModelError(campo, $"El secreto de {nombre} debe empezar por {prefijoEsperado} (mismo ambiente que la llave pública).");
    }
}
