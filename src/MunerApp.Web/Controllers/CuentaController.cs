using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using MunerApp.Domain.Constantes;
using MunerApp.Infrastructure.Identity;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Models.Cuenta;
using MunerApp.Web.Servicios;

namespace MunerApp.Web.Controllers;

public class CuentaController : Controller
{
    private readonly UserManager<Usuario> _userManager;
    private readonly SignInManager<Usuario> _signInManager;
    private readonly MunerAppDbContext _db;
    private readonly InvitacionService _invitaciones;
    private readonly IWebHostEnvironment _entorno;

    public CuentaController(
        UserManager<Usuario> userManager,
        SignInManager<Usuario> signInManager,
        MunerAppDbContext db,
        InvitacionService invitaciones,
        IWebHostEnvironment entorno)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
        _invitaciones = invitaciones;
        _entorno = entorno;
    }

    // ================= HU-001 · Registro de donantes =================

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Registrar(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new RegistroViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Registrar(RegistroViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
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

            // La cuenta queda inactiva hasta confirmar el correo. El enlace lo devuelve a donde iba (HU-016).
            var destino = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : null;
            var envio = await _invitaciones.EnviarConfirmacionAsync(usuario, destino);
            AvisarSiNoSeEnvio(envio);
            TempData["CorreoConfirmacion"] = usuario.Email;
            return RedirectToAction(nameof(ConfirmaTuCorreo));
        }

        if (resultado.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
            ModelState.AddModelError(nameof(model.Email), "Ya existe una cuenta con este correo.");

        foreach (var error in resultado.Errors.Where(e => e.Code is not ("DuplicateEmail" or "DuplicateUserName")))
            ModelState.AddModelError(string.Empty, error.Description);

        return View(model);
    }

    // ================= HU-002 · Inicio de sesión =================

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
            var bloqueo = usuario is null ? "No encontramos tu cuenta." : await ValidarAccesoAsync(usuario);
            if (bloqueo is not null)
            {
                await _signInManager.SignOutAsync();
                ModelState.AddModelError(string.Empty, bloqueo);
                return View(model);
            }
            return await RedirigirSegunRolAsync(usuario!, returnUrl);
        }

        // Cuenta sin confirmar: se ofrece reenviar el correo
        if (resultado.IsNotAllowed)
        {
            var pendiente = await _userManager.FindByEmailAsync(model.Email);
            if (pendiente is not null && !pendiente.EmailConfirmed)
            {
                ViewData["SinConfirmar"] = pendiente.Email;
                ModelState.AddModelError(string.Empty, "Aún no confirmas tu correo. Revisa tu bandeja de entrada o pide un enlace nuevo.");
                return View(model);
            }
        }

        ModelState.AddModelError(string.Empty, resultado.IsLockedOut
            ? "Tu cuenta quedó bloqueada por varios intentos fallidos. Intenta de nuevo en 10 minutos."
            : "Correo o contraseña incorrectos.");
        return View(model);
    }

    // ================= Confirmación del correo =================

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ConfirmaTuCorreo() => View();

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmarCorreo(string? id, string? token, string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(token))
            return View("EnlaceConfirmacionInvalido");

        var usuario = await _userManager.FindByIdAsync(id);
        if (usuario is null) return View("EnlaceConfirmacionInvalido");

        if (!usuario.EmailConfirmed)
        {
            string tokenDecodificado;
            try
            {
                tokenDecodificado = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
            }
            catch (FormatException)
            {
                return View("EnlaceConfirmacionInvalido");
            }

            var resultado = await _userManager.ConfirmEmailAsync(usuario, tokenDecodificado);
            if (!resultado.Succeeded) return View("EnlaceConfirmacionInvalido");
        }

        var bloqueo = await ValidarAccesoAsync(usuario);
        if (bloqueo is not null)
        {
            TempData["Error"] = bloqueo;
            return RedirectToAction(nameof(IniciarSesion));
        }

        await _signInManager.SignInAsync(usuario, isPersistent: false);
        TempData["Mensaje"] = "¡Listo! Confirmaste tu correo y tu cuenta quedó activa.";
        return await RedirigirSegunRolAsync(usuario, returnUrl);
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> ReenviarConfirmacion(string? email)
    {
        var usuario = string.IsNullOrWhiteSpace(email) ? null : await _userManager.FindByEmailAsync(email.Trim());
        if (usuario is not null && usuario.Activo && !usuario.EmailConfirmed)
            AvisarSiNoSeEnvio(await _invitaciones.EnviarConfirmacionAsync(usuario));

        // Mismo mensaje exista o no la cuenta
        TempData["CorreoConfirmacion"] = email;
        return RedirectToAction(nameof(ConfirmaTuCorreo));
    }

    /// <summary>En desarrollo, si no hay SMTP configurado, muestra el enlace para poder probar.</summary>
    private void AvisarSiNoSeEnvio(ResultadoEnvio envio)
    {
        if (!envio.Enviado && _entorno.IsDevelopment())
            TempData["Error"] = $"[Solo en desarrollo] No se pudo enviar el correo; revisa la configuración SMTP. Enlace: {envio.Enlace}";
    }

    // ================= HU-003 · Inicio de sesión con Gmail =================

    [HttpPost]
    [AllowAnonymous]
    public IActionResult LoginExterno(string proveedor, string? returnUrl = null)
    {
        var urlRetorno = Url.Action(nameof(LoginExternoCallback), "Cuenta", new { returnUrl });
        var propiedades = _signInManager.ConfigureExternalAuthenticationProperties(proveedor, urlRetorno);
        return Challenge(propiedades, proveedor);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> LoginExternoCallback(string? returnUrl = null, string? remoteError = null)
    {
        // Escenario 3: el usuario canceló o rechazó la autorización en Google
        if (remoteError is not null)
        {
            TempData["Error"] = "No se completó el inicio de sesión con Google. Puedes intentarlo de nuevo o usar tu correo y contraseña.";
            return RedirectToAction(nameof(IniciarSesion), new { returnUrl });
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info is null)
        {
            TempData["Error"] = "Se canceló el inicio de sesión con Google.";
            return RedirectToAction(nameof(IniciarSesion), new { returnUrl });
        }

        // Escenario 1: cuenta de Google ya vinculada
        var resultado = await _signInManager.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

        if (resultado.Succeeded)
        {
            var vinculado = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            return await FinalizarLoginExternoAsync(vinculado, returnUrl);
        }

        if (resultado.IsLockedOut)
        {
            TempData["Error"] = "Tu cuenta está bloqueada temporalmente. Intenta de nuevo en 10 minutos.";
            return RedirectToAction(nameof(IniciarSesion));
        }

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["Error"] = "Google no compartió tu correo. Intenta con otra cuenta o regístrate con tu correo.";
            return RedirectToAction(nameof(IniciarSesion));
        }

        // Si el correo ya existe (por ejemplo, un administrador invitado), se vincula a esa cuenta.
        // Escenario 2: si no existe, se crea una cuenta nueva con rol Donante.
        var usuario = await _userManager.FindByEmailAsync(email);
        if (usuario is not null && !usuario.EmailConfirmed)
        {
            // La cuenta existe pero nunca se confirmó. Solo se vincula si Google garantiza que el correo es de quien entra;
            // además se borra la contraseña que se puso al registrarse, por si la creó otra persona con ese correo.
            if (!string.Equals(info.Principal.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Google no ha verificado ese correo. Confirma tu cuenta con el enlace que te enviamos al registrarte.";
                return RedirectToAction(nameof(IniciarSesion));
            }
            usuario.EmailConfirmed = true;
            await _userManager.UpdateAsync(usuario);
            if (await _userManager.HasPasswordAsync(usuario))
            {
                await _userManager.RemovePasswordAsync(usuario);
                TempData["Mensaje"] = "Confirmamos tu correo con Google. Por seguridad borramos la contraseña anterior: si quieres entrar con contraseña, usa \"¿Olvidaste tu contraseña?\".";
            }
        }
        if (usuario is null)
        {
            usuario = new Usuario
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                NombreCompleto = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email
            };
            var creado = await _userManager.CreateAsync(usuario);
            if (!creado.Succeeded)
            {
                TempData["Error"] = "No pudimos crear tu cuenta con Google. Intenta registrarte con tu correo.";
                return RedirectToAction(nameof(Registrar));
            }
            await _userManager.AddToRoleAsync(usuario, Roles.Donante);
        }

        var vinculo = await _userManager.AddLoginAsync(usuario, info);
        if (!vinculo.Succeeded)
        {
            TempData["Error"] = "No pudimos vincular tu cuenta de Google. Intenta iniciar sesión con tu correo.";
            return RedirectToAction(nameof(IniciarSesion));
        }

        return await FinalizarLoginExternoAsync(usuario, returnUrl, iniciarSesion: true);
    }

    private async Task<IActionResult> FinalizarLoginExternoAsync(Usuario? usuario, string? returnUrl, bool iniciarSesion = false)
    {
        var bloqueo = usuario is null ? "No encontramos tu cuenta." : await ValidarAccesoAsync(usuario);
        if (bloqueo is not null)
        {
            await _signInManager.SignOutAsync();
            TempData["Error"] = bloqueo;
            return RedirectToAction(nameof(IniciarSesion));
        }

        if (iniciarSesion)
            await _signInManager.SignInAsync(usuario!, isPersistent: false);

        return await RedirigirSegunRolAsync(usuario!, returnUrl);
    }

    // ================= HU-004 · Recuperar contraseña =================

    [HttpGet]
    [AllowAnonymous]
    public IActionResult OlvideContrasena() => View(new OlvideContrasenaViewModel());

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> OlvideContrasena(OlvideContrasenaViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var usuario = await _userManager.FindByEmailAsync(model.Email);
        if (usuario is not null && usuario.Activo)
        {
            var envio = await _invitaciones.EnviarRecuperacionAsync(usuario);
            if (!envio.Enviado && _entorno.IsDevelopment())
                TempData["Error"] = $"[Solo en desarrollo] No se pudo enviar el correo; revisa la configuración SMTP. Enlace: {envio.Enlace}";
        }

        // Por seguridad se muestra el mismo mensaje exista o no la cuenta
        TempData["CorreoRecuperacion"] = model.Email;
        return RedirectToAction(nameof(OlvideContrasenaConfirmacion));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult OlvideContrasenaConfirmacion() => View();

    [HttpGet]
    [AllowAnonymous]
    public IActionResult RestablecerContrasena(string? email = null, string? token = null, bool invitacion = false)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            return View("EnlaceInvalido");

        return View(new RestablecerContrasenaViewModel { Email = email, Token = token, Invitacion = invitacion });
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> RestablecerContrasena(RestablecerContrasenaViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var usuario = await _userManager.FindByEmailAsync(model.Email);
        if (usuario is null) return View("EnlaceInvalido");

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(model.Token));
        }
        catch (FormatException)
        {
            return View("EnlaceInvalido");
        }

        var resultado = await _userManager.ResetPasswordAsync(usuario, token, model.Contrasena);
        if (resultado.Succeeded)
        {
            // El enlace llegó a su correo: eso también prueba que el correo es suyo
            if (!usuario.EmailConfirmed)
            {
                usuario.EmailConfirmed = true;
                await _userManager.UpdateAsync(usuario);
            }

            TempData["Mensaje"] = model.Invitacion
                ? "Tu contraseña quedó creada. Ya puedes iniciar sesión."
                : "Tu contraseña fue actualizada. Ya puedes iniciar sesión.";
            return RedirectToAction(nameof(IniciarSesion));
        }

        // Escenario 3: enlace vencido o ya usado
        if (resultado.Errors.Any(e => e.Code == "InvalidToken"))
            return View("EnlaceInvalido");

        foreach (var error in resultado.Errors)
            ModelState.AddModelError(string.Empty, error.Description);
        return View(model);
    }

    // ================= HU-005 · Cierre de sesión =================

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CerrarSesion()
    {
        await _signInManager.SignOutAsync();
        TempData["Mensaje"] = "Cerraste sesión correctamente. ¡Gracias por pasar!";
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccesoDenegado() => View();

    // ================= Utilidades =================

    /// <summary>Devuelve el motivo si el usuario no puede entrar, o null si puede.</summary>
    private async Task<string?> ValidarAccesoAsync(Usuario usuario)
    {
        if (!usuario.Activo)
            return "Tu cuenta está inactiva. Contacta al administrador de tu fundación.";

        if (usuario.EsalId is int esalId)
        {
            var esalActiva = await _db.Esales.Where(e => e.Id == esalId).Select(e => e.Activa).FirstOrDefaultAsync();
            if (!esalActiva)
                return "Tu fundación está desactivada en la plataforma. Contacta al equipo de MunerApp.";
        }

        return null;
    }

    /// <summary>HU-002 escenario 1: cada rol llega a su panel.</summary>
    private async Task<IActionResult> RedirigirSegunRolAsync(Usuario usuario, string? returnUrl)
    {
        // Mensaje de confirmación del ingreso (observación de pruebas Sprint 1); no pisa uno anterior
        if (TempData.Peek("Mensaje") is null)
        {
            var nombre = (usuario.NombreCompleto ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            TempData["Mensaje"] = string.IsNullOrEmpty(nombre) ? "Iniciaste sesión." : $"Hola, {nombre}. Iniciaste sesión.";
        }

        var esSuperAdmin = await _userManager.IsInRoleAsync(usuario, Roles.SuperAdministrador);
        var esEquipo = await _userManager.IsInRoleAsync(usuario, Roles.AdministradorESAL)
            || await _userManager.IsInRoleAsync(usuario, Roles.Voluntario);

        // El superadmin y el equipo de una fundación entran directo a su panel.
        // Solo se respeta la página de retorno si es una página de su propio panel.
        if (esSuperAdmin || esEquipo)
        {
            var area = esSuperAdmin ? "/Plataforma" : "/Fundacion";
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                && returnUrl.StartsWith(area, StringComparison.OrdinalIgnoreCase))
                return LocalRedirect(returnUrl);
            return esSuperAdmin
                ? RedirectToAction("Index", "Resumen", new { area = "Plataforma" })
                : RedirectToAction("Index", "Panel", new { area = "Fundacion" });
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToAction("Index", "Home");
    }
}
