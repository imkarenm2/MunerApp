using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Servicios;

namespace MunerApp.Web.Controllers;

/// <summary>
/// HU-042: el visitante ve las causas de recaudación de las fundaciones con su progreso.
/// Páginas públicas: no piden sesión. Solo muestran fundaciones activas y datos públicos de cada causa.
/// </summary>
public class CausasController : Controller
{
    private readonly MunerAppDbContext _db;
    private readonly CausasPublicas _causas;

    public CausasController(MunerAppDbContext db, CausasPublicas causas)
    {
        _db = db;
        _causas = causas;
    }

    // Escenario 1: listado de causas de todas las fundaciones
    [HttpGet("causas")]
    public async Task<IActionResult> Index() => View(await _causas.ListarAsync(esalId: null, maxCerradas: 12));

    // Escenarios 2 y 3: detalle (abierta, en pausa o cerrada)
    [HttpGet("fundaciones/{slug}/causas/{id:int}")]
    public async Task<IActionResult> Detalle(string slug, int id)
    {
        var esalId = await _db.Esales.AsNoTracking()
            .Where(e => e.Slug == slug && e.Activa)
            .Select(e => (int?)e.Id)
            .FirstOrDefaultAsync();

        var causa = esalId is int eid ? await _causas.ObtenerAsync(eid, id) : null;
        if (causa is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View("NoDisponible");
        }
        return View(causa);
    }
}
