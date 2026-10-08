using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Entities;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Areas.Fundacion.Models;
using MunerApp.Domain.Enums;
using MunerApp.Web.Seguridad;
using MunerApp.Web.Validacion;

namespace MunerApp.Web.Areas.Fundacion.Controllers;

/// <summary>HU-013: datos oficiales para recibir donaciones por llave o transferencia.</summary>
[Area("Fundacion")]
[Authorize(Policy = Politicas.AdminEsalPrincipal)]
public class DatosDonacionController : Controller
{
    private readonly MunerAppDbContext _db;
    private readonly IEsalActual _esalActual;

    public DatosDonacionController(MunerAppDbContext db, IEsalActual esalActual)
    {
        _db = db;
        _esalActual = esalActual;
    }

    private int EsalId => _esalActual.EsalId ?? throw new InvalidOperationException("El usuario no pertenece a una ESAL.");

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var esal = await _db.Esales.AsNoTracking().FirstAsync(e => e.Id == EsalId);
        var datos = await _db.DatosDonacion.AsNoTracking().FirstOrDefaultAsync();

        var modelo = datos is null
            ? new DatosDonacionViewModel { Titular = esal.Nombre, DocumentoTitular = esal.Nit }
            : new DatosDonacionViewModel
            {
                Titular = datos.Titular,
                DocumentoTitular = datos.DocumentoTitular,
                Entidad = datos.Entidad,
                TipoCuenta = datos.TipoCuenta,
                TipoLlave = datos.TipoLlave,
                Numero = datos.Numero,
                Instrucciones = datos.Instrucciones,
                Configurado = true,
                FechaActualizacion = datos.FechaActualizacion
            };
        modelo.NitEsal = esal.Nit;
        modelo.Slug = esal.Slug;
        return View(modelo);
    }

    [HttpPost]
    public async Task<IActionResult> Index(DatosDonacionViewModel model)
    {
        var esal = await _db.Esales.AsNoTracking().FirstAsync(e => e.Id == EsalId);
        var datos = await _db.DatosDonacion.FirstOrDefaultAsync();

        // La cuenta debe estar a nombre de la fundación: el NIT del titular es siempre el de la ESAL
        model.DocumentoTitular = esal.Nit;

        if (!string.IsNullOrWhiteSpace(model.Entidad) && !ValidadorCuentas.EntidadValida(model.Entidad))
            ModelState.AddModelError(nameof(model.Entidad), "Escribe solo el nombre del banco o la billetera, por ejemplo Bancolombia o Nequi.");

        if (model.TipoCuenta == TipoCuentaDonacion.Llave && model.TipoLlave is null)
            ModelState.AddModelError(nameof(model.TipoLlave), "Selecciona el tipo de llave.");
        if (model.TipoCuenta != TipoCuentaDonacion.Llave)
            model.TipoLlave = null;

        // Observación 7: restricciones por tipo (solo números en cuentas y billeteras, formato legal de cada llave)
        if (model.TipoCuenta is TipoCuentaDonacion tipo && !string.IsNullOrWhiteSpace(model.Numero)
            && (tipo != TipoCuentaDonacion.Llave || model.TipoLlave is not null))
        {
            var (valor, error) = ValidadorCuentas.Validar(tipo, model.TipoLlave, model.Numero, esal.Nit);
            if (error is null) model.Numero = valor;
            else ModelState.AddModelError(nameof(model.Numero), error);
        }

        // Escenario 2: datos incompletos → la validación del modelo resalta los campos
        if (!ModelState.IsValid)
        {
            model.Configurado = datos is not null;
            model.FechaActualizacion = datos?.FechaActualizacion;
            model.NitEsal = esal.Nit;
            model.Slug = esal.Slug;
            return View(model);
        }

        if (datos is null)
        {
            datos = new DatosDonacion { EsalId = EsalId };
            _db.DatosDonacion.Add(datos);
        }

        datos.Titular = model.Titular.Trim();
        datos.DocumentoTitular = esal.Nit;
        datos.Entidad = model.Entidad.Trim();
        datos.TipoCuenta = model.TipoCuenta!.Value;
        datos.TipoLlave = model.TipoLlave;
        datos.Numero = model.Numero.Trim();
        datos.Instrucciones = string.IsNullOrWhiteSpace(model.Instrucciones) ? null : model.Instrucciones.Trim();
        datos.FechaActualizacion = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Escenario 1: se muestran en la opción "Donar" del perfil
        TempData["Mensaje"] = "Guardaste los datos para donar. Ya aparecen en la opción \"Donar\" de tu perfil.";
        return RedirectToAction(nameof(Index));
    }
}
