using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Application.Seguridad;
using MunerApp.Domain.Constantes;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Areas.Fundacion.Models;

namespace MunerApp.Web.Areas.Fundacion.Controllers;

/// <summary>Inicio del panel de la fundación. Muestra solo los módulos activos (HU-007).</summary>
[Area("Fundacion")]
[Authorize(Roles = Roles.AdministradorESAL + "," + Roles.Voluntario)]
public class PanelController : Controller
{
    private readonly MunerAppDbContext _db;
    private readonly IEsalActual _esalActual;
    private readonly IModuloService _modulos;

    public PanelController(MunerAppDbContext db, IEsalActual esalActual, IModuloService modulos)
    {
        _db = db;
        _esalActual = esalActual;
        _modulos = modulos;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (_esalActual.EsalId is not int esalId)
            return RedirectToAction("Index", "Home", new { area = "" });

        var esal = await _db.Esales.AsNoTracking().FirstOrDefaultAsync(e => e.Id == esalId);
        if (esal is null) return NotFound();

        var modulos = await _modulos.ObtenerActivosAsync(esalId);
        var esAdmin = User.IsInRole(Roles.AdministradorESAL);

        var modelo = new PanelViewModel
        {
            NombreEsal = esal.Nombre,
            EsAdmin = esAdmin,
            EsAdminPrincipal = esAdmin && User.HasClaim(MunerAppClaims.Perfil, Perfiles.Principal),
            Perfil = User.FindFirst(MunerAppClaims.Perfil)?.Value,
            Modulos = modulos.Select(m => new ModuloActivo(m.Codigo, m.Nombre, m.EsConfigurable)).ToList(),
            UsuariosActivos = await _db.Users.CountAsync(u => u.EsalId == esalId && u.Activo),
            PasarelaActiva = await _db.ConfigPasarelas.AnyAsync(c => c.Activa),
            RedesConfiguradas = await _db.RedesSociales.CountAsync()
        };

        return View(modelo);
    }
}
