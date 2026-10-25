using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Constantes;
using MunerApp.Web.Areas.Fundacion.Models;
using MunerApp.Web.Seguridad;
using MunerApp.Web.Servicios;
using MunerApp.Web.Validacion;

namespace MunerApp.Web.Areas.Fundacion.Controllers;

/// <summary>
/// Libro de fondos de la fundación: cuánto entró a cada fondo, en qué se gastó y cuánto queda.
/// Todo lo que se registra aquí es público en la veeduría (con su soporte). Los administradores ven;
/// solo el administrador principal registra gastos, traslados, proyectos, anulaciones y cierres.
/// </summary>
[Area("Fundacion")]
[Authorize(Roles = Roles.AdministradorESAL)]
public class FondosController : Controller
{
    /// <summary>No se aceptan gastos de hace más de un año: el libro debe llevarse al día.</summary>
    private const int DiasAtrasMaximo = 366;

    private readonly LibroFondos _libro;
    private readonly IEsalActual _esalActual;

    public FondosController(LibroFondos libro, IEsalActual esalActual)
    {
        _libro = libro;
        _esalActual = esalActual;
    }

    private string UsuarioId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private int EsalId => _esalActual.EsalId ?? throw new InvalidOperationException("El usuario no pertenece a una ESAL.");

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        return View(new FondosIndexViewModel
        {
            Fondos = await _libro.ResumenAsync(EsalId),
            Recientes = await _libro.MovimientosAsync(EsalId, maximo: 10),
            PuedeGestionar = Politicas.EsAdminPrincipal(User)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Detalle(string clave)
    {
        var modelo = await CargarAsync(clave);
        return modelo is null ? NotFound() : View(modelo);
    }

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    [RequestSizeLimit(ValidadorArchivos.LimitePeticionBytes)]
    public async Task<IActionResult> RegistrarGasto(string clave, [Bind(Prefix = "Gasto")] GastoFormViewModel model)
    {
        var valor = ValidarValor(model.Valor, "Gasto.Valor");
        ValidarFecha(model.Fecha, "Gasto.Fecha");
        var soporte = await ValidadorArchivos.ValidarAsync(model.Soporte, TipoArchivo.Documento);
        if (model.Soporte is not { Length: > 0 })
            ModelState.AddModelError("Gasto.Soporte", "Sube la factura, el recibo o una foto: es la evidencia que ve la veeduría.");
        else if (!soporte.Valido)
            ModelState.AddModelError("Gasto.Soporte", soporte.Error!);

        if (!ModelState.IsValid)
        {
            var detalle = await CargarAsync(clave);
            if (detalle is null) return NotFound();
            detalle.Gasto = model;
            return View(nameof(Detalle), detalle);
        }

        var r = await _libro.RegistrarGastoAsync(EsalId, clave, valor!.Value, model.Concepto, model.Fecha!.Value, model.Proveedor, model.Soporte!, soporte, UsuarioId);
        TempData[r.Exito ? "Mensaje" : "Error"] = r.Mensaje;
        return RedirectToAction(nameof(Detalle), new { clave });
    }

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> Trasladar(string clave, [Bind(Prefix = "Traslado")] TrasladoFormViewModel model)
    {
        var valor = ValidarValor(model.Valor, "Traslado.Valor");
        if (!ModelState.IsValid)
        {
            var detalle = await CargarAsync(clave);
            if (detalle is null) return NotFound();
            detalle.Traslado = model;
            return View(nameof(Detalle), detalle);
        }

        var r = await _libro.TrasladarAsync(EsalId, clave, model.Destino, valor!.Value, model.Concepto, Formatos.Hoy(), UsuarioId);
        TempData[r.Exito ? "Mensaje" : "Error"] = r.Mensaje;
        return RedirectToAction(nameof(Detalle), new { clave });
    }

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> Anular(int id, string clave, string? motivo)
    {
        motivo = motivo?.Trim();
        if (string.IsNullOrEmpty(motivo) || motivo.Length < 5)
        {
            TempData["Error"] = "Escribe por qué anulas el movimiento (por ejemplo: el valor quedó mal escrito).";
            return RedirectToAction(nameof(Detalle), new { clave });
        }
        var r = await _libro.AnularAsync(EsalId, id, motivo.Length > 300 ? motivo[..300] : motivo, UsuarioId);
        TempData[r.Exito ? "Mensaje" : "Error"] = r.Mensaje;
        return RedirectToAction(nameof(Detalle), new { clave });
    }

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> Cerrar(string clave)
    {
        var r = await _libro.CerrarAsync(EsalId, clave, UsuarioId);
        TempData[r.Exito ? "Mensaje" : "Error"] = r.Mensaje;
        return RedirectToAction(nameof(Detalle), new { clave });
    }

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> CrearProyecto([Bind(Prefix = "Proyecto")] ProyectoFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault() ?? "Revisa los datos del proyecto.";
            return RedirectToAction(nameof(Index));
        }
        var fondo = await _libro.CrearProyectoAsync(EsalId, model.Nombre, model.Descripcion, UsuarioId);
        TempData["Mensaje"] = $"Creaste el proyecto \"{fondo.Nombre}\". Trasládale dinero desde otro fondo para empezar a usarlo.";
        return RedirectToAction(nameof(Detalle), new { clave = LibroFondos.ClaveDe(fondo) });
    }

    // ---------- Apoyo ----------

    private async Task<FondoDetalleViewModel?> CargarAsync(string clave)
    {
        if (string.IsNullOrEmpty(clave)) return null;
        var fondos = await _libro.ResumenAsync(EsalId);
        var fondo = fondos.FirstOrDefault(f => f.Clave == clave);
        if (fondo is null) return null;
        return new FondoDetalleViewModel
        {
            Fondo = fondo,
            Movimientos = await _libro.MovimientosAsync(EsalId, clave),
            Destinos = fondos.Where(f => f.Clave != clave && f.Abierto).ToList(),
            PuedeGestionar = Politicas.EsAdminPrincipal(User),
            Gasto = new GastoFormViewModel { Fecha = Formatos.Hoy() }
        };
    }

    private decimal? ValidarValor(string? texto, string campo)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null; // ya lo marca [Required]
        var valor = Formatos.LeerPesos(texto);
        if (valor is null or <= 0) { ModelState.AddModelError(campo, "Escribe un valor en pesos mayor que cero."); return null; }
        if (valor > 1_000_000_000) { ModelState.AddModelError(campo, "El valor es demasiado alto. Revísalo."); return null; }
        return valor;
    }

    private void ValidarFecha(DateTime? fecha, string campo)
    {
        if (fecha is not DateTime f) return;
        if (f.Date > Formatos.Hoy()) ModelState.AddModelError(campo, "La fecha no puede ser futura.");
        else if (f.Date < Formatos.Hoy().AddDays(-DiasAtrasMaximo)) ModelState.AddModelError(campo, "La fecha no puede ser de hace más de un año.");
    }
}
