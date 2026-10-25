using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MunerApp.Domain.Constantes;
using MunerApp.Infrastructure.Identity;
using MunerApp.Web.Areas.Plataforma.Models;
using MunerApp.Web.Servicios;

namespace MunerApp.Web.Areas.Plataforma.Controllers;

/// <summary>
/// Veeduría ciudadana. La veeduría pública (/veeduria) la ve cualquiera sin cuenta. Ser veedor es un permiso extra
/// sobre una cuenta normal: el superadministrador lo da por correo y el veedor sigue pudiendo donar. El permiso solo
/// agrega el registro de cambios de cada fundación (sin datos personales de donantes) y cada consulta queda anotada.
/// El equipo de una fundación no puede ser veedor: nadie se vigila a sí mismo.
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

    /// <summary>
    /// Si el correo ya tiene cuenta, le agrega el permiso de veedor (y se le avisa). Si no, crea la cuenta y le llega
    /// un correo para crear su contraseña.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Crear(VeedorFormViewModel model)
    {
        model.Correo = model.Correo?.Trim() ?? "";
        if (!ModelState.IsValid) return View(model);

        var existente = await _usuarios.FindByEmailAsync(model.Correo);
        if (existente is not null)
        {
            if (existente.EsalId is not null
                || await _usuarios.IsInRoleAsync(existente, Roles.AdministradorESAL)
                || await _usuarios.IsInRoleAsync(existente, Roles.Voluntario)
                || await _usuarios.IsInRoleAsync(existente, Roles.SuperAdministrador))
            {
                ModelState.AddModelError(nameof(model.Correo),
                    "Este correo pertenece al equipo de una fundación o de la plataforma. Un veedor no puede vigilar a su propia organización: usa un correo personal.");
                return View(model);
            }
            if (await _usuarios.IsInRoleAsync(existente, Roles.Veedor))
            {
                ModelState.AddModelError(nameof(model.Correo), "Esta cuenta ya es veedor.");
                return View(model);
            }
            if (!existente.Activo)
            {
                ModelState.AddModelError(nameof(model.Correo), "Esta cuenta está desactivada. Actívala primero.");
                return View(model);
            }

            existente.Organizacion = model.Organizacion.Trim();
            await _usuarios.UpdateAsync(existente);
            await _usuarios.AddToRoleAsync(existente, Roles.Veedor);
            // Así la sesión abierta toma el permiso nuevo en máximo 1 minuto
            await _usuarios.UpdateSecurityStampAsync(existente);

            var aviso = await _invitaciones.EnviarAvisoVeedorAsync(existente);
            TempData["Mensaje"] = aviso.Enviado
                ? $"{existente.NombreCompleto} ya tenía cuenta: ahora también es veedor. Le avisamos por correo."
                : $"{existente.NombreCompleto} ya tenía cuenta: ahora también es veedor.";
            return RedirectToAction(nameof(Index));
        }

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
            ? $"Creaste la cuenta de {veedor.NombreCompleto} con permiso de veedor. Le enviamos un correo para crear su contraseña."
            : $"Creaste la cuenta de {veedor.NombreCompleto} con permiso de veedor.";
        if (!envio.Enviado)
            TempData["Error"] = _entorno.IsDevelopment()
                ? $"[Solo en desarrollo] No se pudo enviar el correo; revisa la configuración SMTP. Enlace para crear la contraseña: {envio.Enlace}"
                : "No se pudo enviar el correo. El veedor puede usar \"¿Olvidaste tu contraseña?\".";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Quita el permiso de veedor. La cuenta sigue existiendo (puede seguir donando). Aplica en máximo 1 minuto.</summary>
    [HttpPost]
    public async Task<IActionResult> QuitarPermiso(string id)
    {
        var veedor = await _usuarios.FindByIdAsync(id);
        if (veedor is null || !await _usuarios.IsInRoleAsync(veedor, Roles.Veedor)) return NotFound();

        await _usuarios.RemoveFromRoleAsync(veedor, Roles.Veedor);
        await _usuarios.UpdateSecurityStampAsync(veedor);

        TempData["Mensaje"] = $"{veedor.NombreCompleto} ya no es veedor. Su cuenta sigue activa.";
        return RedirectToAction(nameof(Index));
    }
}
