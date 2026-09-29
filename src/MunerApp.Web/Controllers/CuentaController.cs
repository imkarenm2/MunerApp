using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MunerApp.Domain.Constantes;
using MunerApp.Infrastructure.Identity;
using MunerApp.Web.Models.Cuenta;

namespace MunerApp.Web.Controllers;

public class CuentaController : Controller
{
    private readonly UserManager<Usuario> _userManager;
    private readonly SignInManager<Usuario> _signInManager;

    public CuentaController(UserManager<Usuario> userManager, SignInManager<Usuario> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    // ---------------- HU-001 · Registro de donantes (Karen) ----------------

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Registrar() => View(new RegistroViewModel());

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Registrar(RegistroViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var usuario = new Usuario
        {
            UserName = model.Email,
            Email = model.Email,
            NombreCompleto = model.NombreCompleto.Trim()
        };

        var resultado = await _userManager.CreateAsync(usuario, model.Contrasena);
        if (resultado.Succeeded)
        {
            await _userManager.AddToRoleAsync(usuario, Roles.Donante);
            TempData["Mensaje"] = "Tu cuenta fue creada. Ya puedes iniciar sesión.";
            return RedirectToAction(nameof(IniciarSesion));
        }

        // Duplicado de correo: un solo mensaje (Identity reporta usuario y correo por separado)
        if (resultado.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
            ModelState.AddModelError(nameof(model.Email), "Ya existe una cuenta con este correo.");

        foreach (var error in resultado.Errors.Where(e => e.Code is not ("DuplicateEmail" or "DuplicateUserName")))
            ModelState.AddModelError(string.Empty, error.Description);

        return View(model);
    }

    // ---------------- HU-002 · Inicio de sesión (Jailer) ----------------

    [HttpGet]
    [AllowAnonymous]
    public IActionResult IniciarSesion(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new InicioSesionViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> IniciarSesion(InicioSesionViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View(model);

        var resultado = await _signInManager.PasswordSignInAsync(
            model.Email, model.Contrasena, model.Recordarme, lockoutOnFailure: true);

        if (resultado.Succeeded)
        {
            var usuario = await _userManager.FindByEmailAsync(model.Email);
            if (usuario is null || !usuario.Activo)
            {
                await _signInManager.SignOutAsync();
                ModelState.AddModelError(string.Empty, "Tu cuenta está inactiva. Contacta al administrador de tu fundación.");
                return View(model);
            }
            return await RedirigirSegunRolAsync(usuario, returnUrl);
        }

        ModelState.AddModelError(string.Empty, resultado.IsLockedOut
            ? "Tu cuenta quedó bloqueada por varios intentos fallidos. Intenta de nuevo en 10 minutos."
            : "Correo o contraseña incorrectos.");
        return View(model);
    }

    // ---------------- HU-005 · Cierre de sesión (Jailer) ----------------

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CerrarSesion()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccesoDenegado() => View();

    // ---------------- Pendientes del Sprint 1 ----------------
    // TODO HU-003 (Santiago): LoginExterno (Challenge a Google) y LoginExternoCallback
    //      -> si el correo existe, iniciar sesión; si no, crear Usuario con rol Donante.
    // TODO HU-004 (Santiago): OlvideContrasena, RestablecerContrasena con
    //      _userManager.GeneratePasswordResetTokenAsync / ResetPasswordAsync e ICorreoService.

    private async Task<IActionResult> RedirigirSegunRolAsync(Usuario usuario, string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        // TODO HU-002 (Jailer): redirigir a cada panel cuando existan, por ejemplo:
        // if (await _userManager.IsInRoleAsync(usuario, Roles.SuperAdministrador))
        //     return RedirectToAction("Index", "Esales", new { area = "Plataforma" });
        // AdministradorESAL / Voluntario -> panel de su ESAL; Donante -> inicio.
        await Task.CompletedTask;
        return RedirectToAction("Index", "Home");
    }
}
