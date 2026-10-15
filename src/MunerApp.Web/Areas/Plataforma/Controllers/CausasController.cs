using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Constantes;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Areas.Plataforma.Models;
using MunerApp.Web.Servicios;

namespace MunerApp.Web.Areas.Plataforma.Controllers;

/// <summary>
/// Causas de toda la plataforma. Las que proponen las fundaciones llegan "Por aprobar": el superadministrador
/// lee la justificación y las aprueba (se publican) o las devuelve con un motivo. También puede crear una causa
/// a nombre de una fundación, que se publica de una vez.
/// </summary>
[Area("Plataforma")]
[Authorize(Roles = Roles.SuperAdministrador)]
public class CausasController : Controller
{
    private readonly MunerAppDbContext _db;
    private readonly IAlmacenamientoArchivos _archivos;
    private readonly INotificacionService _notificaciones;
    private readonly AvisosCorreo _avisos;

    public CausasController(MunerAppDbContext db, IAlmacenamientoArchivos archivos, INotificacionService notificaciones, AvisosCorreo avisos)
    {
        _db = db;
        _archivos = archivos;
        _notificaciones = notificaciones;
        _avisos = avisos;
    }

    private string UsuarioId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // ---------- Listado ----------

    [HttpGet]
    public async Task<IActionResult> Index(FiltroCausas filtro = FiltroCausas.PorAprobar)
    {
        var publicos = Causa.EstadosPublicos;
        var conteos = new Dictionary<FiltroCausas, int>
        {
            [FiltroCausas.PorAprobar] = await _db.Causas.CountAsync(c => c.Estado == EstadoCausa.PorAprobar),
            [FiltroCausas.Publicadas] = await _db.Causas.CountAsync(c => publicos.Contains(c.Estado)),
            [FiltroCausas.Rechazadas] = await _db.Causas.CountAsync(c => c.Estado == EstadoCausa.Rechazada),
            [FiltroCausas.Todas] = await _db.Causas.CountAsync()
        };

        var consulta = _db.Causas.AsNoTracking().AsQueryable();
        consulta = filtro switch
        {
            FiltroCausas.PorAprobar => consulta.Where(c => c.Estado == EstadoCausa.PorAprobar),
            FiltroCausas.Publicadas => consulta.Where(c => publicos.Contains(c.Estado)),
            FiltroCausas.Rechazadas => consulta.Where(c => c.Estado == EstadoCausa.Rechazada),
            _ => consulta
        };

        var hoy = DateTime.Today;
        var causas = await consulta
            .OrderBy(c => c.Estado == EstadoCausa.PorAprobar ? 0 : 1)
            .ThenByDescending(c => c.FechaCreacion)
            .Take(200)
            .Select(c => new
            {
                Causa = c,
                Fundacion = c.Esal!.Nombre,
                Slug = c.Esal.Slug,
                Recaudado = _db.Donaciones.Where(d => d.CausaId == c.Id && d.Estado == EstadoDonacion.Confirmada).Sum(d => (decimal?)d.Valor) ?? 0,
                DeFundacion = _db.Users.Any(u => u.Id == c.CreadaPorId && u.EsalId == c.EsalId)
            })
            .ToListAsync();

        return View(new CausasPlataformaViewModel
        {
            Filtro = filtro,
            Conteos = conteos,
            Causas = causas.Select(x => new CausaPlataformaItem
            {
                Id = x.Causa.Id,
                Titulo = x.Causa.Titulo,
                Fundacion = x.Fundacion,
                SlugFundacion = x.Slug,
                Meta = x.Causa.Meta,
                Recaudado = x.Recaudado,
                FechaLimite = x.Causa.FechaLimite,
                FechaCreacion = x.Causa.FechaCreacion,
                Estado = x.Causa.Estado,
                Cerrada = x.Causa.EsPublica && x.Causa.EstaCerrada(x.Recaudado, hoy),
                CreadaPorPlataforma = !x.DeFundacion
            }).ToList()
        });
    }

    // ---------- Revisar, aprobar y rechazar ----------

    [HttpGet]
    public async Task<IActionResult> Revisar(int id)
    {
        var modelo = await CargarRevisionAsync(id);
        return modelo is null ? NotFound() : View(modelo);
    }

    [HttpPost]
    public async Task<IActionResult> Aprobar(int id)
    {
        var c = await _db.Causas.Include(x => x.Esal).FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();

        if (c.Estado != EstadoCausa.PorAprobar)
        {
            TempData["Error"] = $"\"{c.Titulo}\" no está esperando aprobación.";
            return RedirectToAction(nameof(Revisar), new { id });
        }
        if (c.FechaLimite.Date <= DateTime.Today)
        {
            TempData["Error"] = "La fecha límite de la causa ya pasó. Devuélvela para que la fundación ponga una fecha nueva.";
            return RedirectToAction(nameof(Revisar), new { id });
        }
        if (c.Esal is null || !c.Esal.Activa)
        {
            TempData["Error"] = "La fundación está desactivada: no se puede publicar la causa.";
            return RedirectToAction(nameof(Revisar), new { id });
        }

        c.Estado = EstadoCausa.Activa;
        c.MotivoRechazo = null;
        c.RevisadaPorId = UsuarioId;
        c.FechaRevision = DateTime.UtcNow;
        await _notificaciones.AgregarAAdministradoresAsync(c.EsalId, "Causa aprobada",
            $"El equipo de MunerApp aprobó \"{c.Titulo}\". Ya está publicada y recibe donaciones.", "/Fundacion/Causas", "bi-check2-circle");
        await _db.SaveChangesAsync();
        await AvisarCreadorAsync(c, "Tu causa fue aprobada",
            $"\"{c.Titulo}\" ya está publicada en el perfil de la fundación y puede recibir donaciones.");

        TempData["Mensaje"] = $"Aprobaste \"{c.Titulo}\". Ya está publicada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Rechazar(int id, RevisarCausaViewModel form)
    {
        var c = await _db.Causas.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();

        if (c.Estado != EstadoCausa.PorAprobar)
        {
            TempData["Error"] = $"\"{c.Titulo}\" no está esperando aprobación.";
            return RedirectToAction(nameof(Revisar), new { id });
        }

        var motivo = form.Motivo?.Trim() ?? "";
        if (motivo.Length < 10)
            ModelState.AddModelError(nameof(form.Motivo), "Explica qué debe corregir la fundación (mínimo 10 caracteres).");
        if (!ModelState.IsValid)
        {
            var modelo = await CargarRevisionAsync(id);
            modelo!.Motivo = form.Motivo;
            return View(nameof(Revisar), modelo);
        }

        c.Estado = EstadoCausa.Rechazada;
        c.MotivoRechazo = motivo;
        c.RevisadaPorId = UsuarioId;
        c.FechaRevision = DateTime.UtcNow;
        await _notificaciones.AgregarAAdministradoresAsync(c.EsalId, "Una causa necesita cambios",
            $"\"{c.Titulo}\": {motivo}", "/Fundacion/Causas", "bi-arrow-counterclockwise");
        await _db.SaveChangesAsync();
        await AvisarCreadorAsync(c, "Tu causa necesita cambios",
            $"El equipo de MunerApp revisó \"{c.Titulo}\" y pidió esto: {motivo}. Corrígela desde el panel y volverá a revisión.");

        TempData["Mensaje"] = $"Devolviste \"{c.Titulo}\" a la fundación con tus observaciones.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Crear a nombre de una fundación ----------

    [HttpGet]
    public async Task<IActionResult> Crear(int? esal)
    {
        var modelo = new CausaPlataformaFormViewModel { EsalId = esal, FechaLimite = DateTime.Today.AddDays(30) };
        return View(await ConFundacionesAsync(modelo));
    }

    [HttpPost]
    [RequestSizeLimit(FormularioCausa.LimitePeticion)]
    public async Task<IActionResult> Crear(CausaPlataformaFormViewModel model)
    {
        model.PideJustificacion = false;
        var meta = FormularioCausa.ValidarCampos(ModelState, model, recaudado: 0);
        var validadas = await FormularioCausa.ValidarFotosAsync(ModelState, FormularioCausa.FotosEnviadas(model), 0, exigirUna: true);

        Esal? esal = null;
        if (model.EsalId is int esalId)
        {
            esal = await _db.Esales.FirstOrDefaultAsync(e => e.Id == esalId && e.Activa);
            if (esal is null)
                ModelState.AddModelError(nameof(model.EsalId), "Selecciona una fundación activa.");
            else if (!await _db.DatosDonacion.AnyAsync(d => d.EsalId == esalId))
                ModelState.AddModelError(nameof(model.EsalId), "Esa fundación aún no tiene datos para donar: nadie podría aportar a la causa.");
        }
        if (!ModelState.IsValid) return View(await ConFundacionesAsync(model));

        var causa = new Causa
        {
            EsalId = esal!.Id,
            Titulo = model.Titulo.Trim(),
            Descripcion = model.Descripcion.Trim(),
            Meta = meta!.Value,
            FechaLimite = model.FechaLimite!.Value.Date,
            Estado = EstadoCausa.Activa,
            CreadaPorId = UsuarioId,
            RevisadaPorId = UsuarioId,
            FechaRevision = DateTime.UtcNow
        };
        await FormularioCausa.AgregarFotosAsync(_archivos, causa, validadas, ordenInicial: 0);
        _db.Causas.Add(causa);
        await _notificaciones.AgregarAAdministradoresAsync(esal.Id, "MunerApp publicó una causa para ustedes",
            $"Se publicó \"{causa.Titulo}\" en el perfil de la fundación, con meta de {Formatos.Pesos(causa.Meta)}.", "/Fundacion/Causas", "bi-bullseye");
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"Publicaste \"{causa.Titulo}\" para {esal.Nombre}.";
        return RedirectToAction(nameof(Index), new { filtro = FiltroCausas.Publicadas });
    }

    // ---------- Apoyo ----------

    private async Task<RevisarCausaViewModel?> CargarRevisionAsync(int id)
    {
        var c = await _db.Causas.AsNoTracking().Include(x => x.Esal).Include(x => x.Fotos).FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return null;

        var publicos = Causa.EstadosPublicos;
        var recaudado = await _db.Donaciones.Where(d => d.CausaId == id && d.Estado == EstadoDonacion.Confirmada).SumAsync(d => (decimal?)d.Valor) ?? 0;
        var nombres = await _db.Users.Where(u => u.Id == c.CreadaPorId || u.Id == c.RevisadaPorId)
            .Select(u => new { u.Id, u.NombreCompleto }).ToDictionaryAsync(u => u.Id, u => u.NombreCompleto);

        return new RevisarCausaViewModel
        {
            Id = c.Id,
            Titulo = c.Titulo,
            Descripcion = c.Descripcion,
            Justificacion = c.Justificacion,
            Meta = c.Meta,
            Recaudado = recaudado,
            FechaLimite = c.FechaLimite,
            FechaCreacion = c.FechaCreacion,
            Estado = c.Estado,
            Cerrada = c.EsPublica && c.EstaCerrada(recaudado, DateTime.Today),
            MotivoRechazo = c.MotivoRechazo,
            FechaRevision = c.FechaRevision,
            RevisadaPor = c.RevisadaPorId is null ? null : nombres.GetValueOrDefault(c.RevisadaPorId),
            CreadaPor = c.CreadaPorId is null ? null : nombres.GetValueOrDefault(c.CreadaPorId),
            Fotos = c.Fotos.OrderBy(f => f.Orden).ThenBy(f => f.Id).Select(f => _archivos.UrlPublica(f.Ruta)).ToList(),
            EsalId = c.EsalId,
            Fundacion = c.Esal!.Nombre,
            SlugFundacion = c.Esal.Slug,
            CiudadFundacion = c.Esal.Ciudad,
            FundacionActiva = c.Esal.Activa,
            FundacionTieneDatosDonacion = await _db.DatosDonacion.AnyAsync(d => d.EsalId == c.EsalId),
            CausasAnteriores = await _db.Causas.CountAsync(x => x.EsalId == c.EsalId && x.Id != c.Id && publicos.Contains(x.Estado))
        };
    }

    private async Task<CausaPlataformaFormViewModel> ConFundacionesAsync(CausaPlataformaFormViewModel model)
    {
        model.Fundaciones = await _db.Esales.AsNoTracking()
            .Where(e => e.Activa)
            .OrderBy(e => e.Nombre)
            .Select(e => new SelectListItem(e.Nombre, e.Id.ToString()))
            .ToListAsync();
        return model;
    }

    /// <summary>Correo a quien propuso la causa (si fue alguien de la fundación). Es de cortesía: si falla no pasa nada.</summary>
    private async Task AvisarCreadorAsync(Causa c, string titulo, string mensaje)
    {
        if (string.IsNullOrEmpty(c.CreadaPorId)) return;
        await _avisos.EnviarAUsuarioAsync(c.CreadaPorId, $"{titulo}: {c.Titulo}", titulo, mensaje, "Ver mis causas", "/Fundacion/Causas");
    }
}
