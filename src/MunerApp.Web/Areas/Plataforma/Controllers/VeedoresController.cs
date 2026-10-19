using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunerApp.Domain.Constantes;
using MunerApp.Infrastructure.Identity;
using MunerApp.Web.Areas.Plataforma.Models;
using MunerApp.Web.Servicios;

namespace MunerApp.Web.Areas.Plataforma.Controllers;

/// <summary>
/// Cuentas de veeduría ciudadana. Las crea el superadministrador: el veedor recibe un correo para crear su contraseña
/// y solo puede consultar la información de transparencia de las fundaciones (sin datos personales de donantes).
/// </summary>
[Area("Plataforma")]
[Authorize(Roles = Roles.SuperAdministrador)]
public class VeedoresController : Controller
{
    private readonly UserManager<Usuario> _usuarios;
    private readonly InvitacionService _invitaciones;
    private readonly IWebHostEnvironment _entorno;

    public VeedoresController(UserManager<Usuario> usuarios, InvitacionService invitaciones, IWebHostEnvironment entorno)
    {
        _usuarios = usuarios;
        _invitaciones = invitaciones;
        _entorno = entorno;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var veedores = await _usuarios.GetUsersInRoleAsync(Roles.Veedor);
        return View(veedores.OrderBy(v => v.NombreCompleto).Select(v => new VeedorItem
        {
            Id = v.Id, Nombre = v.NombreCompleto, Correo = v.Email ?? "", Organizacion = v.Organizacion,
            Activo = v.Activo, FechaRegistro = v.FechaRegistro
        }).ToList());
    }

    [HttpGet]
    public IActionResult Crear() => View(new VeedorFormViewModel());

    [HttpPost]
    public async Task<IActionResult> Crear(VeedorFormViewModel model)
    {
        model.Correo = model.Correo?.Trim() ?? "";
        if (!string.IsNullOrEmpty(model.Correo) && await _usuarios.FindByEmailAsync(model.Correo) is not null)
            ModelState.AddModelError(nameof(model.Correo), "Ya existe una cuenta con este correo en la plataforma.");
        if (!ModelState.IsValid) return View(model);

        var veedor = new Usuario
        {
            UserName = model.Correo,
            Email = model.Correo,
            EmailConfirmed = true, // crea su contraseña desde el enlace que le llega al correo
            NombreCompleto = model.Nombre.Trim(),
            Organizacion = model.Organizacion.Trim()
        };
        var creado = await _usuarios.CreateAsync(veedor);
        if (!creado.Succeeded)
        {
            foreach (var e in creado.Errors) ModelState.AddModelError(string.Empty, e.Description);
            return View(model);
        }
        await _usuarios.AddToRoleAsync(veedor, Roles.Veedor);

        var envio = await _invitaciones.EnviarInvitacionVeedorAsync(veedor);
        TempData["Mensaje"] = envio.Enviado
            ? $"Creaste el acceso de {veedor.NombreCompleto}. Le enviamos un correo para crear su contraseña."
            : $"Creaste el acceso de {veedor.NombreCompleto}.";
        if (!envio.Enviado)
            TempData["Error"] = _entorno.IsDevelopment()
                ? $"[Solo en desarrollo] No se pudo enviar el correo; revisa la configuración SMTP. Enlace para crear la contraseña: {envio.Enlace}"
                : "No se pudo enviar el correo. El veedor puede usar \"¿Olvidaste tu contraseña?\".";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Activa o desactiva el acceso. Al desactivarlo, su sesión se cierra en máximo 1 minuto.</summary>
    [HttpPost]
    public async Task<IActionResult> CambiarEstado(string id)
    {
        var veedor = await _usuarios.FindByIdAsync(id);
        if (veedor is null || !await _usuarios.IsInRoleAsync(veedor, Roles.Veedor)) return NotFound();

        veedor.Activo = !veedor.Activo;
        await _usuarios.UpdateAsync(veedor);
        await _usuarios.UpdateSecurityStampAsync(veedor);

        TempData["Mensaje"] = veedor.Activo
            ? $"{veedor.NombreCompleto} vuelve a tener acceso a la veeduría."
            : $"Desactivaste el acceso de {veedor.NombreCompleto}.";
        return RedirectToAction(nameof(Index));
    }
}
