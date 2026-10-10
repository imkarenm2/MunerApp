using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Application.Seguridad;
using MunerApp.Domain.Constantes;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Areas.Fundacion.Models;
using MunerApp.Web.Seguridad;
using MunerApp.Web.Servicios;

namespace MunerApp.Web.Areas.Fundacion.Controllers;

/// <summary>Inicio del panel de la fundación. Muestra solo los módulos activos (HU-007).</summary>
[Area("Fundacion")]
[Authorize(Roles = Roles.AdministradorESAL + "," + Roles.Voluntario)]
public class PanelController : Controller
{
    private readonly MunerAppDbContext _db;
    private readonly IEsalActual _esalActual;
    private readonly IModuloService _modulos;
    private readonly AlertasSalud _alertas;

    public PanelController(MunerAppDbContext db, IEsalActual esalActual, IModuloService modulos, AlertasSalud alertas)
    {
        _db = db;
        _esalActual = esalActual;
        _modulos = modulos;
        _alertas = alertas;
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
            RedesConfiguradas = await _db.RedesSociales.CountAsync(),

            // Sprint 2
            Slug = esal.Slug,
            PerfilCompleto = PorcentajePerfil(esal, await _db.FotosEsal.AnyAsync()),
            DocumentosVisibles = await _db.DocumentosTransparencia.CountAsync(d => d.Visible),
            DatosDonacionConfigurados = await _db.DatosDonacion.AnyAsync(),
            // Solo las transferencias esperan revisión: los pagos en línea los confirma el aviso de Wompi (HU-044)
            DonacionesPendientes = esAdmin ? await _db.Donaciones.CountAsync(d => d.Estado == EstadoDonacion.Pendiente && d.Origen == OrigenDonacion.Manual) : 0,
            PostulacionesPendientes = esAdmin ? await _db.PostulacionesVoluntario.CountAsync(p => p.Estado == EstadoPostulacion.Pendiente) : 0,

            // HU-039: alertas de salud, solo para los responsables y con el módulo de salud activo
            AlertasSalud = Politicas.TieneAccesoClinico(User) && modulos.Any(m => m.Codigo == CodigosModulo.Salud)
                ? await _alertas.ResumenAsync(esalId) : null,
            TieneAgenda = modulos.Any(m => m.Codigo == CodigosModulo.Beneficiarios)
        };

        return View(modelo);
    }

    /// <summary>Qué tan completo está el perfil público (para animar a la fundación a terminarlo).</summary>
    private static int PorcentajePerfil(Domain.Entities.Esal e, bool tieneFotos)
    {
        var campos = new[]
        {
            !string.IsNullOrWhiteSpace(e.DescripcionCorta), !string.IsNullOrWhiteSpace(e.Historia),
            !string.IsNullOrWhiteSpace(e.Mision), !string.IsNullOrWhiteSpace(e.Vision),
            !string.IsNullOrWhiteSpace(e.Ciudad), !string.IsNullOrWhiteSpace(e.LogoRuta), tieneFotos
        };
        return (int)Math.Round(100.0 * campos.Count(c => c) / campos.Length);
    }
}
