using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunerApp.Infrastructure.Persistence;

namespace MunerApp.Web.Controllers;

/// <summary>Notificaciones internas (sistema base del Sprint 2, HU-015).</summary>
[Authorize]
[Route("notificaciones")]
public class NotificacionesController : Controller
{
    private readonly MunerAppDbContext _db;

    public NotificacionesController(MunerAppDbContext db) => _db = db;

    private string UsuarioId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var notificaciones = await _db.Notificaciones.AsNoTracking()
            .Where(n => n.UsuarioId == UsuarioId)
            .OrderByDescending(n => n.Fecha)
            .Take(50)
            .ToListAsync();
        return View(notificaciones);
    }

    /// <summary>Marca la notificación como leída y lleva a la página relacionada.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Abrir(int id)
    {
        var n = await _db.Notificaciones.FirstOrDefaultAsync(x => x.Id == id && x.UsuarioId == UsuarioId);
        if (n is null) return RedirectToAction(nameof(Index));

        if (!n.Leida)
        {
            n.Leida = true;
            await _db.SaveChangesAsync();
        }
        return !string.IsNullOrEmpty(n.Url) && Url.IsLocalUrl(n.Url) ? LocalRedirect(n.Url) : RedirectToAction(nameof(Index));
    }

    [HttpPost("leer-todas")]
    public async Task<IActionResult> LeerTodas()
    {
        await _db.Notificaciones
            .Where(n => n.UsuarioId == UsuarioId && !n.Leida)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.Leida, true));
        TempData["Mensaje"] = "Marcaste todas las notificaciones como leídas.";
        return RedirectToAction(nameof(Index));
    }
}
