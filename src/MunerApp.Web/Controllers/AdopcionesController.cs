using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Application.Seguridad;
using MunerApp.Domain.Constantes;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Models.Publico;
using MunerApp.Web.Validacion;

namespace MunerApp.Web.Controllers;

/// <summary>
/// Solicitud de adopción en línea. HU-029: la persona lee y acepta las recomendaciones
/// y responsabilidades; solo entonces se habilita el formulario por secciones (HU-030 a HU-032),
/// que guarda el avance en cada una.
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

    /// <summary>Lleva a la persona a la sección donde quedó su solicitud.</summary>
    [HttpGet("fundaciones/{slug}/adoptar/formulario")]
    public async Task<IActionResult> Formulario(string slug)
    {
        var (_, borrador, salida) = await AbrirSeccionAsync(slug, 1);
        if (salida is not null) return salida;

        return borrador!.SeccionesCompletadas switch
        {
            0 => RedirectToAction(nameof(Datos), new { slug }),
            _ => RedirectToAction(nameof(Mascotas), new { slug })
        };
    }

    // ---------- HU-030: sección 1, datos personales y de contacto ----------

    [HttpGet("fundaciones/{slug}/adoptar/formulario/datos")]
    public async Task<IActionResult> Datos(string slug)
    {
        var (esal, s, salida) = await AbrirSeccionAsync(slug, 1);
        if (salida is not null) return salida;

        var model = new DatosPersonalesAdopcionViewModel
        {
            NombreCompleto = s!.NombreCompleto ?? User.FindFirstValue(MunerAppClaims.NombreCompleto) ?? "",
            Cedula = s.Cedula ?? "",
            Edad = s.Edad,
            Celular = s.Celular ?? "",
            Ciudad = s.Ciudad ?? "",
            Direccion = s.Direccion ?? "",
            Ocupacion = s.Ocupacion,
            DetalleOcupacion = s.DetalleOcupacion,
            ReferenciaNombre = s.ReferenciaNombre ?? "",
            ReferenciaCelular = s.ReferenciaCelular ?? "",
            ReferenciaRelacion = s.ReferenciaRelacion ?? ""
        };
        return View(PrepararSeccion(model, esal!, s, 1));
    }

    [HttpPost("fundaciones/{slug}/adoptar/formulario/datos")]
    public async Task<IActionResult> Datos(string slug, DatosPersonalesAdopcionViewModel model)
    {
        var (esal, s, salida) = await AbrirSeccionAsync(slug, 1);
        if (salida is not null) return salida;

        // Escenario 3: cédula y celulares con formato válido (la edad mínima la valida el modelo)
        var cedula = ValidadorPersonas.Cedula(model.Cedula);
        if (!string.IsNullOrWhiteSpace(model.Cedula) && cedula is null)
            ModelState.AddModelError(nameof(model.Cedula), ValidadorPersonas.ErrorCedula);

        var celular = ValidadorPersonas.Celular(model.Celular);
        if (!string.IsNullOrWhiteSpace(model.Celular) && celular is null)
            ModelState.AddModelError(nameof(model.Celular), ValidadorPersonas.ErrorCelular);

        var celularReferencia = ValidadorPersonas.Celular(model.ReferenciaCelular);
        if (!string.IsNullOrWhiteSpace(model.ReferenciaCelular) && celularReferencia is null)
            ModelState.AddModelError(nameof(model.ReferenciaCelular), ValidadorPersonas.ErrorCelular);
        else if (celularReferencia is not null && celularReferencia == celular)
            ModelState.AddModelError(nameof(model.ReferenciaCelular), "La referencia debe ser otra persona: escribe un celular diferente al tuyo.");

        if (!string.IsNullOrEmpty(model.ReferenciaRelacion) && !DatosPersonalesAdopcionViewModel.Relaciones.Contains(model.ReferenciaRelacion))
            ModelState.AddModelError(nameof(model.ReferenciaRelacion), "Selecciona una relación de la lista.");

        // Escenario 2: si es independiente, debe indicar a qué se dedica
        if (model.Ocupacion == OcupacionAdoptante.Independiente && string.IsNullOrWhiteSpace(model.DetalleOcupacion))
            ModelState.AddModelError(nameof(model.DetalleOcupacion), "Cuéntanos a qué te dedicas como independiente.");

        if (!ModelState.IsValid) return View(PrepararSeccion(model, esal!, s!, 1));

        // Escenario 1: se guarda el avance y se pasa a la sección de mascotas
        s!.NombreCompleto = model.NombreCompleto.Trim();
        s.Cedula = cedula;
        s.Edad = model.Edad;
        s.Celular = celular;
        s.Ciudad = model.Ciudad.Trim();
        s.Direccion = model.Direccion.Trim();
        s.Ocupacion = model.Ocupacion;
        s.DetalleOcupacion = model.Ocupacion == OcupacionAdoptante.Independiente ? model.DetalleOcupacion!.Trim() : null;
        s.ReferenciaNombre = model.ReferenciaNombre.Trim();
        s.ReferenciaCelular = celularReferencia;
        s.ReferenciaRelacion = model.ReferenciaRelacion;
        s.SeccionesCompletadas = Math.Max(s.SeccionesCompletadas, 1);
        s.FechaActualizacion = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = "Guardamos tus datos personales. Sigue con la sección de mascotas.";
        return RedirectToAction(nameof(Mascotas), new { slug });
    }

    // ---------- HU-031: sección 2, mascotas ----------

    [HttpGet("fundaciones/{slug}/adoptar/formulario/mascotas")]
    public async Task<IActionResult> Mascotas(string slug)
    {
        var (esal, s, salida) = await AbrirSeccionAsync(slug, 2);
        if (salida is not null) return salida;
        return View(PrepararSeccion(new SeccionAdopcionViewModel(), esal!, s!, 2));
    }

    /// <summary>
    /// Valida que se pueda abrir la sección: fundación con adopción activa, borrador creado al aceptar
    /// las recomendaciones (escenario 2 de HU-029) y secciones anteriores guardadas (no se saltan secciones).
    /// </summary>
    private async Task<(Esal? Esal, SolicitudAdopcion? Borrador, IActionResult? Salida)> AbrirSeccionAsync(string slug, int seccion)
    {
        var esal = await BuscarConAdopcionAsync(slug);
        if (esal is null) return (null, null, ModuloNoDisponible());

        var borrador = await BuscarBorradorAsync(esal.Id);
        if (borrador is null)
        {
            TempData["Error"] = "Antes de diligenciar el formulario debes leer y aceptar las recomendaciones.";
            return (esal, null, RedirectToAction(nameof(Recomendaciones), new { slug }));
        }

        if (seccion > borrador.SeccionesCompletadas + 1)
            return (esal, borrador, RedirectToAction(nameof(Formulario), new { slug }));

        return (esal, borrador, null);
    }

    private T PrepararSeccion<T>(T model, Esal esal, SolicitudAdopcion borrador, int seccion) where T : SeccionAdopcionViewModel
    {
        model.Slug = esal.Slug!;
        model.NombreEsal = esal.Nombre;
        model.LogoUrl = UrlArchivo(esal.LogoRuta);
        model.Seccion = seccion;
        model.SeccionesCompletadas = borrador.SeccionesCompletadas;
        if (model is DatosPersonalesAdopcionViewModel datos)
            datos.Correo = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "";
        return model;
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
