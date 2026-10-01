using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Areas.Fundacion.Models;
using MunerApp.Web.Seguridad;

namespace MunerApp.Web.Areas.Fundacion.Controllers;

/// <summary>HU-011: redes sociales de la fundación.</summary>
[Area("Fundacion")]
[Authorize(Policy = Politicas.AdminEsalPrincipal)]
public class RedesController : Controller
{
    private static readonly Dictionary<TipoRedSocial, string[]> DominiosPermitidos = new()
    {
        [TipoRedSocial.Facebook] = new[] { "facebook.com", "fb.com" },
        [TipoRedSocial.Instagram] = new[] { "instagram.com" },
        [TipoRedSocial.TikTok] = new[] { "tiktok.com" }
    };

    private readonly MunerAppDbContext _db;
    private readonly IEsalActual _esalActual;

    public RedesController(MunerAppDbContext db, IEsalActual esalActual)
    {
        _db = db;
        _esalActual = esalActual;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // El filtro global ya limita a las redes de la ESAL del usuario
        var redes = await _db.RedesSociales.AsNoTracking().ToListAsync();
        return View(new RedesViewModel
        {
            Facebook = redes.FirstOrDefault(r => r.Tipo == TipoRedSocial.Facebook)?.Url,
            Instagram = redes.FirstOrDefault(r => r.Tipo == TipoRedSocial.Instagram)?.Url,
            TikTok = redes.FirstOrDefault(r => r.Tipo == TipoRedSocial.TikTok)?.Url
        });
    }

    [HttpPost]
    public async Task<IActionResult> Index(RedesViewModel model)
    {
        var valores = new Dictionary<TipoRedSocial, (string Campo, string? Url)>
        {
            [TipoRedSocial.Facebook] = (nameof(model.Facebook), model.Facebook?.Trim()),
            [TipoRedSocial.Instagram] = (nameof(model.Instagram), model.Instagram?.Trim()),
            [TipoRedSocial.TikTok] = (nameof(model.TikTok), model.TikTok?.Trim())
        };

        // Escenario 2: enlace inválido
        foreach (var (tipo, (campo, url)) in valores)
        {
            if (!string.IsNullOrEmpty(url) && !EsUrlValida(url, DominiosPermitidos[tipo]))
                ModelState.AddModelError(campo, $"Ingresa un enlace válido de {tipo}, por ejemplo https://{DominiosPermitidos[tipo][0]}/tufundacion");
        }
        if (!ModelState.IsValid) return View(model);

        var esalId = _esalActual.EsalId ?? throw new InvalidOperationException("El usuario no pertenece a una ESAL.");
        var existentes = await _db.RedesSociales.ToListAsync();

        foreach (var (tipo, (_, url)) in valores)
        {
            var registro = existentes.FirstOrDefault(r => r.Tipo == tipo);
            if (string.IsNullOrEmpty(url))
            {
                // Escenario 3: una red sin enlace no se muestra
                if (registro is not null) _db.RedesSociales.Remove(registro);
            }
            else if (registro is null)
            {
                _db.RedesSociales.Add(new RedSocial { EsalId = esalId, Tipo = tipo, Url = url });
            }
            else
            {
                registro.Url = url;
            }
        }

        await _db.SaveChangesAsync();
        TempData["Mensaje"] = "Guardaste las redes sociales de la fundación.";
        return RedirectToAction(nameof(Index));
    }

    private static bool EsUrlValida(string url, string[] dominios)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) return false;
        var host = uri.Host.ToLowerInvariant();
        return dominios.Any(d => host == d || host.EndsWith("." + d));
    }
}
