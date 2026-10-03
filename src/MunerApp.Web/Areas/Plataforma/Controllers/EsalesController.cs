using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunerApp.Domain.Constantes;
using MunerApp.Domain.Entities;
using MunerApp.Infrastructure.Identity;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Areas.Plataforma.Models;
using MunerApp.Web.Servicios;

namespace MunerApp.Web.Areas.Plataforma.Controllers;

/// <summary>HU-006 (registro de ESAL) y HU-007 (módulos configurables). Solo superadministrador.</summary>
[Area("Plataforma")]
[Authorize(Roles = Roles.SuperAdministrador)]
public class EsalesController : Controller
{
    private readonly MunerAppDbContext _db;
    private readonly UserManager<Usuario> _userManager;
    private readonly InvitacionService _invitaciones;
    private readonly IWebHostEnvironment _entorno;

    public EsalesController(MunerAppDbContext db, UserManager<Usuario> userManager, InvitacionService invitaciones, IWebHostEnvironment entorno)
    {
        _db = db;
        _userManager = userManager;
        _invitaciones = invitaciones;
        _entorno = entorno;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var esales = await _db.Esales.AsNoTracking()
            .OrderBy(e => e.Nombre)
            .Select(e => new EsalListaItem
            {
                Id = e.Id,
                Nombre = e.Nombre,
                Nit = e.Nit,
                TipoEntidad = e.TipoEntidad,
                Activa = e.Activa,
                FechaRegistro = e.FechaRegistro,
                ModulosActivos = e.Modulos.Count(m => m.Activo),
                Usuarios = _db.Users.Count(u => u.EsalId == e.Id)
            })
            .ToListAsync();

        return View(esales);
    }

    // ---------- HU-006 escenario 1 y 2: registrar ESAL con su administrador ----------

    [HttpGet]
    public IActionResult Crear() => View(new EsalCrearViewModel());

    [HttpPost]
    public async Task<IActionResult> Crear(EsalCrearViewModel model)
    {
        Normalizar(model);
        model.AdminEmail = model.AdminEmail?.Trim() ?? string.Empty;

        await ValidarDuplicadosAsync(model);

        if (!string.IsNullOrEmpty(model.AdminEmail) && await _userManager.FindByEmailAsync(model.AdminEmail) is not null)
            ModelState.AddModelError(nameof(model.AdminEmail), "Ya existe un usuario con este correo en la plataforma.");

        if (!TiposEntidad.Todos.Contains(model.TipoEntidad))
            ModelState.AddModelError(nameof(model.TipoEntidad), "Selecciona un tipo de entidad de la lista.");

        if (!ModelState.IsValid) return View(model);

        await using var transaccion = await _db.Database.BeginTransactionAsync();

        var esal = new Esal
        {
            Nombre = model.Nombre,
            Nit = model.Nit,
            TipoEntidad = model.TipoEntidad,
            CorreoContacto = model.CorreoContacto
        };

        // Los módulos configurables nacen desactivados; se activan después del levantamiento (HU-007)
        var configurables = await _db.Modulos.Where(m => m.EsConfigurable).ToListAsync();
        foreach (var modulo in configurables)
            esal.Modulos.Add(new EsalModulo { ModuloId = modulo.Id, Activo = false });

        _db.Esales.Add(esal);
        await _db.SaveChangesAsync();

        var admin = new Usuario
        {
            UserName = model.AdminEmail,
            Email = model.AdminEmail,
            EmailConfirmed = true,
            NombreCompleto = model.AdminNombre.Trim(),
            EsalId = esal.Id,
            Perfil = Perfiles.Principal
        };

        var creado = await _userManager.CreateAsync(admin);
        if (!creado.Succeeded)
        {
            await transaccion.RollbackAsync();
            foreach (var error in creado.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        await _userManager.AddToRoleAsync(admin, Roles.AdministradorESAL);
        await transaccion.CommitAsync();

        var envio = await _invitaciones.EnviarInvitacionAsync(admin, esal.Nombre);
        TempData["Mensaje"] = envio.Enviado
            ? $"Registraste a {esal.Nombre}. Le enviamos a {admin.Email} un correo para crear su contraseña. Ahora activa sus módulos."
            : $"Registraste a {esal.Nombre}. Ahora activa sus módulos.";
        if (!envio.Enviado)
            TempData["Error"] = MensajeCorreoNoEnviado(envio);

        return RedirectToAction(nameof(Modulos), new { id = esal.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var esal = await _db.Esales.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        if (esal is null) return NotFound();

        return View(new EsalDatosViewModel
        {
            Id = esal.Id,
            Nombre = esal.Nombre,
            Nit = esal.Nit,
            TipoEntidad = esal.TipoEntidad,
            CorreoContacto = esal.CorreoContacto
        });
    }

    [HttpPost]
    public async Task<IActionResult> Editar(EsalDatosViewModel model)
    {
        var esal = await _db.Esales.FirstOrDefaultAsync(e => e.Id == model.Id);
        if (esal is null) return NotFound();

        Normalizar(model);
        await ValidarDuplicadosAsync(model);
        if (!TiposEntidad.Todos.Contains(model.TipoEntidad))
            ModelState.AddModelError(nameof(model.TipoEntidad), "Selecciona un tipo de entidad de la lista.");
        if (!ModelState.IsValid) return View(model);

        esal.Nombre = model.Nombre;
        esal.Nit = model.Nit;
        esal.TipoEntidad = model.TipoEntidad;
        esal.CorreoContacto = model.CorreoContacto;
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"Actualizaste los datos de {esal.Nombre}.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- HU-006 escenario 3: activar / desactivar ESAL ----------

    [HttpPost]
    public async Task<IActionResult> CambiarEstado(int id)
    {
        var esal = await _db.Esales.FirstOrDefaultAsync(e => e.Id == id);
        if (esal is null) return NotFound();

        esal.Activa = !esal.Activa;
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = esal.Activa
            ? $"{esal.Nombre} quedó activa."
            : $"{esal.Nombre} quedó desactivada: su perfil no es visible y sus usuarios no pueden ingresar.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- HU-007: módulos configurables ----------

    [HttpGet]
    public async Task<IActionResult> Modulos(int id)
    {
        var esal = await _db.Esales.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        if (esal is null) return NotFound();

        var activos = await _db.EsalModulos.AsNoTracking()
            .Where(em => em.EsalId == id && em.Activo)
            .Select(em => em.ModuloId)
            .ToListAsync();

        var modulos = await _db.Modulos.AsNoTracking()
            .Where(m => m.EsConfigurable)
            .OrderBy(m => m.Id)
            .Select(m => new ModuloItem { ModuloId = m.Id, Codigo = m.Codigo, Nombre = m.Nombre })
            .ToListAsync();

        foreach (var m in modulos) m.Activo = activos.Contains(m.ModuloId);

        return View(new ModulosEsalViewModel { EsalId = esal.Id, EsalNombre = esal.Nombre, Modulos = modulos });
    }

    [HttpPost]
    public async Task<IActionResult> Modulos(ModulosEsalViewModel model)
    {
        var esal = await _db.Esales.AsNoTracking().FirstOrDefaultAsync(e => e.Id == model.EsalId);
        if (esal is null) return NotFound();

        bool Activo(string codigo) => model.Modulos.Any(m => m.Codigo == codigo && m.Activo);
        if ((Activo(CodigosModulo.Adopcion) || Activo(CodigosModulo.Salud)) && !Activo(CodigosModulo.Beneficiarios))
        {
            ModelState.AddModelError(string.Empty, "Adopción y Salud necesitan que Beneficiarios esté activo.");
            model.EsalNombre = esal.Nombre;
            return View(model);
        }

        var existentes = await _db.EsalModulos.Where(em => em.EsalId == model.EsalId).ToListAsync();
        foreach (var item in model.Modulos)
        {
            var registro = existentes.FirstOrDefault(em => em.ModuloId == item.ModuloId);
            if (registro is null)
            {
                _db.EsalModulos.Add(new EsalModulo { EsalId = model.EsalId, ModuloId = item.ModuloId, Activo = item.Activo });
            }
            else if (registro.Activo != item.Activo)
            {
                // Desactivar no borra datos (HU-007 escenario 2)
                registro.Activo = item.Activo;
                registro.FechaCambio = DateTime.UtcNow;
            }
        }
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"Guardaste los módulos de {esal.Nombre}.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Observación de pruebas Sprint 1: no permitir fundaciones duplicadas ----------

    private static void Normalizar(EsalDatosViewModel model)
    {
        // Quita espacios repetidos para que "Reino  de los Gatos " cuente igual que "Reino de los Gatos"
        model.Nombre = string.Join(' ', (model.Nombre ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        model.Nit = model.Nit?.Trim() ?? string.Empty;
        model.CorreoContacto = model.CorreoContacto?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    /// <summary>
    /// Una fundación no puede repetir el NIT, el nombre ni el correo de contacto de otra
    /// (sin importar mayúsculas). En Editar se excluye la misma fundación.
    /// </summary>
    private async Task ValidarDuplicadosAsync(EsalDatosViewModel model)
    {
        var otras = _db.Esales.AsNoTracking().Where(e => e.Id != model.Id);
        var nombre = model.Nombre.ToLower();
        var correo = model.CorreoContacto.ToLower();

        if (!string.IsNullOrEmpty(model.Nit) && await otras.AnyAsync(e => e.Nit == model.Nit))
            ModelState.AddModelError(nameof(model.Nit), "Ya existe una fundación registrada con este NIT.");

        if (!string.IsNullOrEmpty(nombre) && await otras.AnyAsync(e => e.Nombre.ToLower() == nombre))
            ModelState.AddModelError(nameof(model.Nombre), "Ya existe una fundación registrada con este nombre.");

        if (!string.IsNullOrEmpty(correo) && await otras.AnyAsync(e => e.CorreoContacto.ToLower() == correo))
            ModelState.AddModelError(nameof(model.CorreoContacto), "Este correo ya es el contacto de otra fundación.");
    }

    private string MensajeCorreoNoEnviado(ResultadoEnvio envio) => _entorno.IsDevelopment()
        ? $"[Solo en desarrollo] No se pudo enviar el correo de invitación; revisa la configuración SMTP. Enlace para crear la contraseña: {envio.Enlace}"
        : "No se pudo enviar el correo de invitación. El administrador puede usar \"¿Olvidaste tu contraseña?\" o entrar con Google.";
}
