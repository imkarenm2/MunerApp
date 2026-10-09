using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Constantes;
using MunerApp.Infrastructure.Identity;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Models.Publico;
using MunerApp.Web.Servicios;

namespace MunerApp.Web.Controllers;

/// <summary>
/// "Quiero participar": una fundación pide entrar a MunerApp. La solicitud le llega al superadministrador
/// como notificación y por correo; él la contacta, hace el levantamiento y la registra (HU-006).
/// </summary>
public class ParticiparController : Controller
{
    private readonly UserManager<Usuario> _userManager;
    private readonly INotificacionService _notificaciones;
    private readonly MunerAppDbContext _db;
    private readonly AvisosCorreo _avisos;

    public ParticiparController(UserManager<Usuario> userManager, INotificacionService notificaciones,
        MunerAppDbContext db, AvisosCorreo avisos)
    {
        _userManager = userManager;
        _notificaciones = notificaciones;
        _db = db;
        _avisos = avisos;
    }

    [HttpGet("participar")]
    public IActionResult Index() => View(new ParticiparViewModel());

    [HttpPost("participar")]
    public async Task<IActionResult> Index(ParticiparViewModel model)
    {
        if (!string.IsNullOrEmpty(model.SitioWeb)) return RedirectToAction(nameof(Enviada)); // robot

        if (!string.IsNullOrEmpty(model.TipoEntidad) && !TiposEntidad.Todos.Contains(model.TipoEntidad))
            ModelState.AddModelError(nameof(model.TipoEntidad), "Selecciona una opción de la lista.");
        if (!string.IsNullOrWhiteSpace(model.Ciudad) && !Region.EsMunicipioValido(model.Ciudad))
            ModelState.AddModelError(nameof(model.Ciudad), $"Por ahora solo trabajamos con fundaciones de {Region.Nombre}. Selecciona un municipio de la lista.");
        if (!ModelState.IsValid) return View(model);

        var resumen = $"{model.NombreFundacion.Trim()} ({model.TipoEntidad}, {model.Ciudad.Trim()}). " +
                      $"Contacto: {model.NombreContacto.Trim()} · {model.Correo.Trim()} · {model.Telefono.Trim()}" +
                      (string.IsNullOrWhiteSpace(model.Mensaje) ? "" : $". \"{model.Mensaje.Trim()}\"");

        var superadmins = await _userManager.GetUsersInRoleAsync(Roles.SuperAdministrador);
        foreach (var admin in superadmins)
            _notificaciones.Agregar(admin.Id, "Una fundación quiere participar", resumen, "/Plataforma/Esales/Crear", "bi-building-add");
        await _db.SaveChangesAsync();

        foreach (var admin in superadmins.Where(a => !string.IsNullOrEmpty(a.Email)))
            await _avisos.EnviarAsync(admin.Email!, $"Solicitud para participar: {model.NombreFundacion.Trim()}",
                "Una fundación quiere participar en MunerApp", resumen, "Registrar la fundación", "/Plataforma/Esales/Crear");

        return RedirectToAction(nameof(Enviada));
    }

    [HttpGet("participar/enviada")]
    public IActionResult Enviada() => View();
}
