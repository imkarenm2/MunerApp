using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Constantes;
using MunerApp.Infrastructure.Identity;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Areas.Fundacion.Models;
using MunerApp.Web.Seguridad;
using MunerApp.Web.Servicios;

namespace MunerApp.Web.Areas.Fundacion.Controllers;

/// <summary>
/// HU-008: usuarios y roles de la fundación.
/// Todas las consultas filtran por la ESAL del administrador (escenario 3: aislamiento entre ESALes).
/// </summary>
[Area("Fundacion")]
[Authorize(Roles = Roles.AdministradorESAL)]
public class UsuariosController : Controller
{
    private readonly MunerAppDbContext _db;
    private readonly UserManager<Usuario> _userManager;
    private readonly IEsalActual _esalActual;
    private readonly InvitacionService _invitaciones;
    private readonly IWebHostEnvironment _entorno;

    public UsuariosController(
        MunerAppDbContext db,
        UserManager<Usuario> userManager,
        IEsalActual esalActual,
        InvitacionService invitaciones,
        IWebHostEnvironment entorno)
    {
        _db = db;
        _userManager = userManager;
        _esalActual = esalActual;
        _invitaciones = invitaciones;
        _entorno = entorno;
    }

    private int EsalId => _esalActual.EsalId ?? throw new InvalidOperationException("El usuario no pertenece a una ESAL.");

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var idActual = _userManager.GetUserId(User);
        var usuarios = await (
            from u in _db.Users
            where u.EsalId == EsalId
            join ur in _db.UserRoles on u.Id equals ur.UserId
            join r in _db.Roles on ur.RoleId equals r.Id
            where r.Name == Roles.AdministradorESAL || r.Name == Roles.Voluntario
            orderby u.NombreCompleto
            select new UsuarioEsalItem
            {
                Id = u.Id,
                NombreCompleto = u.NombreCompleto,
                Email = u.Email!,
                Rol = r.Name!,
                Perfil = u.Perfil,
                Activo = u.Activo,
                EsUsuarioActual = u.Id == idActual
            }).ToListAsync();

        ViewData["PuedeGestionar"] = (await EsAdminPrincipalAsync());
        return View(usuarios);
    }

    // ---------- Escenario 1: crear usuario ----------

    [HttpGet]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public IActionResult Crear() => View(new UsuarioEsalFormViewModel());

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> Crear(UsuarioEsalFormViewModel model)
    {
        model.Email = model.Email?.Trim() ?? string.Empty;
        if (!OpcionValida(model.RolPerfil))
            ModelState.AddModelError(nameof(model.RolPerfil), "Selecciona un rol de la lista.");
        if (!string.IsNullOrEmpty(model.Email) && await _userManager.FindByEmailAsync(model.Email) is not null)
            ModelState.AddModelError(nameof(model.Email), "Ya existe un usuario con este correo en la plataforma.");
        if (!ModelState.IsValid) return View(model);

        var (rol, perfil) = Separar(model.RolPerfil);
        var usuario = new Usuario
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true,
            NombreCompleto = model.NombreCompleto.Trim(),
            EsalId = EsalId,
            Perfil = perfil
        };

        var creado = await _userManager.CreateAsync(usuario);
        if (!creado.Succeeded)
        {
            foreach (var error in creado.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }
        await _userManager.AddToRoleAsync(usuario, rol);

        var nombreEsal = await _db.Esales.Where(e => e.Id == EsalId).Select(e => e.Nombre).FirstAsync();
        var envio = await _invitaciones.EnviarInvitacionAsync(usuario, nombreEsal);

        TempData["Mensaje"] = envio.Enviado
            ? $"Agregaste a {usuario.NombreCompleto} como {UsuarioEsalFormViewModel.TextoDe(model.RolPerfil).ToLower()}. Le enviamos un correo para crear su contraseña."
            : $"Agregaste a {usuario.NombreCompleto} como {UsuarioEsalFormViewModel.TextoDe(model.RolPerfil).ToLower()}.";
        if (!envio.Enviado) TempData["Error"] = MensajeCorreoNoEnviado(envio);

        return RedirectToAction(nameof(Index));
    }

    // ---------- Escenario 2: cambiar rol ----------

    [HttpGet]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> Editar(string id)
    {
        var usuario = await BuscarDeMiEsalAsync(id);
        if (usuario is null) return NotFound();

        var rol = (await _userManager.GetRolesAsync(usuario))
            .FirstOrDefault(r => r is Roles.AdministradorESAL or Roles.Voluntario) ?? string.Empty;

        return View(new UsuarioEsalFormViewModel
        {
            Id = usuario.Id,
            NombreCompleto = usuario.NombreCompleto,
            Email = usuario.Email!,
            RolPerfil = $"{rol}|{usuario.Perfil}"
        });
    }

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> Editar(UsuarioEsalFormViewModel model)
    {
        var usuario = await BuscarDeMiEsalAsync(model.Id);
        if (usuario is null) return NotFound();

        // El correo no se cambia desde aquí
        ModelState.Remove(nameof(model.Email));
        model.Email = usuario.Email!;

        if (!OpcionValida(model.RolPerfil))
            ModelState.AddModelError(nameof(model.RolPerfil), "Selecciona un rol de la lista.");
        if (usuario.Id == _userManager.GetUserId(User) && model.RolPerfil != $"{Roles.AdministradorESAL}|{Perfiles.Principal}")
            ModelState.AddModelError(nameof(model.RolPerfil), "No puedes quitarte a ti mismo el rol de administrador principal.");
        if (!ModelState.IsValid) return View(model);

        var (rol, perfil) = Separar(model.RolPerfil);
        var rolesActuales = (await _userManager.GetRolesAsync(usuario))
            .Where(r => r is Roles.AdministradorESAL or Roles.Voluntario)
            .ToList();

        if (!rolesActuales.SequenceEqual(new[] { rol }))
        {
            await _userManager.RemoveFromRolesAsync(usuario, rolesActuales);
            await _userManager.AddToRoleAsync(usuario, rol);
        }

        usuario.NombreCompleto = model.NombreCompleto.Trim();
        usuario.Perfil = perfil;
        await _userManager.UpdateAsync(usuario);

        // La sesión del usuario se revalida con los nuevos permisos (máximo 1 minuto)
        await _userManager.UpdateSecurityStampAsync(usuario);

        TempData["Mensaje"] = $"Actualizaste a {usuario.NombreCompleto}. Tendrá los nuevos permisos la próxima vez que ingrese.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Activar / desactivar ----------

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> CambiarEstado(string id)
    {
        var usuario = await BuscarDeMiEsalAsync(id);
        if (usuario is null) return NotFound();

        if (usuario.Id == _userManager.GetUserId(User))
        {
            TempData["Error"] = "No puedes desactivar tu propia cuenta.";
            return RedirectToAction(nameof(Index));
        }

        usuario.Activo = !usuario.Activo;
        await _userManager.UpdateAsync(usuario);
        if (!usuario.Activo) await _userManager.UpdateSecurityStampAsync(usuario); // cierra su sesión

        TempData["Mensaje"] = usuario.Activo
            ? $"{usuario.NombreCompleto} puede volver a ingresar."
            : $"Desactivaste a {usuario.NombreCompleto}: ya no puede ingresar a la plataforma.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Utilidades ----------

    /// <summary>Solo encuentra usuarios de la misma ESAL; si el id es de otra fundación devuelve null (escenario 3).</summary>
    private Task<Usuario?> BuscarDeMiEsalAsync(string? id) =>
        string.IsNullOrEmpty(id)
            ? Task.FromResult<Usuario?>(null)
            : _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.EsalId == EsalId);

    private async Task<bool> EsAdminPrincipalAsync()
    {
        var usuario = await _userManager.GetUserAsync(User);
        return usuario?.Perfil == Perfiles.Principal;
    }

    private static bool OpcionValida(string? valor) =>
        UsuarioEsalFormViewModel.Opciones.Any(o => o.Valor == valor);

    private static (string Rol, string Perfil) Separar(string rolPerfil)
    {
        var partes = rolPerfil.Split('|');
        return (partes[0], partes[1]);
    }

    private string MensajeCorreoNoEnviado(ResultadoEnvio envio) => _entorno.IsDevelopment()
        ? $"[Solo en desarrollo] No se pudo enviar el correo; revisa la configuración SMTP. Enlace para crear la contraseña: {envio.Enlace}"
        : "No se pudo enviar el correo de invitación. La persona puede usar \"¿Olvidaste tu contraseña?\" o entrar con Google.";
}
