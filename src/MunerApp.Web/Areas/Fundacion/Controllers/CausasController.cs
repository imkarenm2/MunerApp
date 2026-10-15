using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Application.Seguridad;
using MunerApp.Domain.Constantes;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Identity;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Areas.Fundacion.Models;
using MunerApp.Web.Seguridad;
using MunerApp.Web.Servicios;
using MunerApp.Web.Validacion;

namespace MunerApp.Web.Areas.Fundacion.Controllers;

/// <summary>
/// HU-041: causas de recaudación (vakis). Los administradores de la ESAL ven el listado; proponer, editar y pausar
/// es del administrador principal. Cada causa nueva lleva una justificación y queda "Por aprobar" hasta que el
/// superadministrador la revisa: solo entonces se publica. El filtro global por ESAL impide tocar causas de otra fundación.
/// </summary>
[Area("Fundacion")]
[Authorize(Roles = Roles.AdministradorESAL)]
public class CausasController : Controller
{
    private const long LimitePeticion = FormularioCausa.LimitePeticion;

    private readonly MunerAppDbContext _db;
    private readonly IEsalActual _esalActual;
    private readonly IAlmacenamientoArchivos _archivos;
    private readonly UserManager<Usuario> _usuarios;
    private readonly INotificacionService _notificaciones;
    private readonly AvisosCorreo _avisos;

    public CausasController(MunerAppDbContext db, IEsalActual esalActual, IAlmacenamientoArchivos archivos,
        UserManager<Usuario> usuarios, INotificacionService notificaciones, AvisosCorreo avisos)
    {
        _db = db;
        _esalActual = esalActual;
        _archivos = archivos;
        _usuarios = usuarios;
        _notificaciones = notificaciones;
        _avisos = avisos;
    }

    private string UsuarioId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private int EsalId => _esalActual.EsalId ?? throw new InvalidOperationException("El usuario no pertenece a una ESAL.");

    // ---------- Listado ----------

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var causas = await _db.Causas.AsNoTracking()
            .Include(c => c.Fotos)
            .OrderBy(c => c.Estado == EstadoCausa.Cerrada).ThenByDescending(c => c.FechaCreacion)
            .ToListAsync();
        var recaudos = await RecaudadoPorCausaAsync();
        var hoy = DateTime.Today;

        return View(new CausasIndexViewModel
        {
            Slug = await _db.Esales.AsNoTracking().Where(e => e.Id == EsalId).Select(e => e.Slug).FirstOrDefaultAsync(),
            PuedeGestionar = User.HasClaim(MunerAppClaims.Perfil, Perfiles.Principal),
            Causas = causas
                .Select(c => Item(c, recaudos.GetValueOrDefault(c.Id), hoy))
                .OrderByDescending(c => c.EnRevision).ThenBy(c => c.Cerrada).ThenByDescending(c => c.Id)
                .ToList()
        });
    }

    // ---------- Escenario 1 y 2: publicar ----------

    [HttpGet]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> Crear()
    {
        if (!await _db.DatosDonacion.AnyAsync()) return SinDatosParaDonar();
        return View(new CausaFormViewModel { FechaLimite = DateTime.Today.AddDays(30), PideJustificacion = true });
    }

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    [RequestSizeLimit(LimitePeticion)]
    public async Task<IActionResult> Crear(CausaFormViewModel model)
    {
        if (!await _db.DatosDonacion.AnyAsync()) return SinDatosParaDonar();

        model.PideJustificacion = true;
        var meta = FormularioCausa.ValidarCampos(ModelState, model, recaudado: 0);

        // Al menos una foto
        var validadas = await FormularioCausa.ValidarFotosAsync(ModelState, FormularioCausa.FotosEnviadas(model), fotosExistentes: 0, exigirUna: true);

        if (!ModelState.IsValid) return View(model);

        var causa = new Causa
        {
            EsalId = EsalId,
            Titulo = model.Titulo.Trim(),
            Descripcion = model.Descripcion.Trim(),
            Justificacion = model.Justificacion!.Trim(),
            Meta = meta!.Value,
            FechaLimite = model.FechaLimite!.Value.Date,
            Estado = EstadoCausa.PorAprobar,
            CreadaPorId = UsuarioId
        };
        await FormularioCausa.AgregarFotosAsync(_archivos, causa, validadas, ordenInicial: 0);
        _db.Causas.Add(causa);
        await _db.SaveChangesAsync();
        await AvisarSuperadminsAsync(causa, corregida: false);

        TempData["Mensaje"] = $"Enviaste \"{causa.Titulo}\" para aprobación. El equipo de MunerApp la revisará y te avisaremos cuando quede publicada.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Escenario 3: editar, pausar y reanudar ----------

    [HttpGet]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> Editar(int id)
    {
        var c = await _db.Causas.AsNoTracking().Include(x => x.Fotos).FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();

        var recaudado = await RecaudadoAsync(id);
        if (c.EstaCerrada(recaudado, DateTime.Today))
        {
            TempData["Error"] = $"\"{c.Titulo}\" ya está cerrada y no se puede editar.";
            return RedirectToAction(nameof(Index));
        }

        return View(Formulario(c, recaudado, new CausaFormViewModel
        {
            Titulo = c.Titulo,
            Descripcion = c.Descripcion,
            Meta = ((long)c.Meta).ToString("N0", new System.Globalization.CultureInfo("es-CO")),
            FechaLimite = c.FechaLimite,
            Justificacion = c.Justificacion
        }));
    }

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    [RequestSizeLimit(LimitePeticion)]
    public async Task<IActionResult> Editar(int id, CausaFormViewModel model)
    {
        var c = await _db.Causas.Include(x => x.Fotos).FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();

        var recaudado = await RecaudadoAsync(id);
        if (c.EstaCerrada(recaudado, DateTime.Today))
        {
            TempData["Error"] = $"\"{c.Titulo}\" ya está cerrada y no se puede editar.";
            return RedirectToAction(nameof(Index));
        }

        model.PideJustificacion = c.EnRevision;
        var meta = FormularioCausa.ValidarCampos(ModelState, model, recaudado);
        var validadas = await FormularioCausa.ValidarFotosAsync(ModelState, FormularioCausa.FotosEnviadas(model), c.Fotos.Count, exigirUna: false);
        if (!ModelState.IsValid) return View(Formulario(c, recaudado, model));

        c.Titulo = model.Titulo.Trim();
        c.Descripcion = model.Descripcion.Trim();
        c.Meta = meta!.Value;
        c.FechaLimite = model.FechaLimite!.Value.Date;
        await FormularioCausa.AgregarFotosAsync(_archivos, c, validadas, ordenInicial: c.Fotos.Count == 0 ? 0 : c.Fotos.Max(f => f.Orden) + 1);

        // Una causa rechazada que se corrige vuelve a revisión
        var reenviada = c.Estado == EstadoCausa.Rechazada;
        if (c.EnRevision)
        {
            c.Justificacion = model.Justificacion!.Trim();
            c.Estado = EstadoCausa.PorAprobar;
            c.MotivoRechazo = null;
        }
        await _db.SaveChangesAsync();
        if (reenviada) await AvisarSuperadminsAsync(c, corregida: true);

        TempData["Mensaje"] = c.Estado == EstadoCausa.PorAprobar
            ? $"Guardaste \"{c.Titulo}\". Sigue en revisión: se publicará cuando el equipo de MunerApp la apruebe."
            : $"Guardaste los cambios de \"{c.Titulo}\". Ya se ven en el perfil.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> EliminarFoto(int id, int fotoId)
    {
        var c = await _db.Causas.Include(x => x.Fotos).FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();

        var foto = c.Fotos.FirstOrDefault(f => f.Id == fotoId);
        if (foto is null) return NotFound();
        if (c.Fotos.Count <= 1)
        {
            TempData["Error"] = "La causa debe tener al menos una foto. Sube otra antes de quitar esta.";
            return RedirectToAction(nameof(Editar), new { id });
        }

        _db.FotosCausa.Remove(foto);
        await _db.SaveChangesAsync();
        await _archivos.EliminarAsync(foto.Ruta);

        TempData["Mensaje"] = "Quitaste la foto.";
        return RedirectToAction(nameof(Editar), new { id });
    }

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public Task<IActionResult> Pausar(int id) => CambiarPausaAsync(id, pausar: true);

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public Task<IActionResult> Reanudar(int id) => CambiarPausaAsync(id, pausar: false);

    private async Task<IActionResult> CambiarPausaAsync(int id, bool pausar)
    {
        var c = await _db.Causas.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();

        if (c.EstaCerrada(await RecaudadoAsync(id), DateTime.Today))
        {
            TempData["Error"] = $"\"{c.Titulo}\" ya está cerrada.";
            return RedirectToAction(nameof(Index));
        }

        // Pausar o reanudar solo aplica a causas ya aprobadas
        if (c.EnRevision)
        {
            TempData["Error"] = $"\"{c.Titulo}\" todavía no está aprobada.";
            return RedirectToAction(nameof(Index));
        }

        var nuevo = pausar ? EstadoCausa.Pausada : EstadoCausa.Activa;
        if (c.Estado == nuevo)
        {
            TempData["Error"] = $"\"{c.Titulo}\" ya estaba {(pausar ? "en pausa" : "activa")}.";
            return RedirectToAction(nameof(Index));
        }

        c.Estado = nuevo;
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = pausar
            ? $"Pausaste \"{c.Titulo}\". Se sigue viendo, pero no recibe donaciones hasta que la reanudes."
            : $"Reanudaste \"{c.Titulo}\". Vuelve a recibir donaciones.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Apoyo ----------

    /// <summary>Sin cuenta oficial, los donantes no tendrían a dónde transferir para la causa.</summary>
    private IActionResult SinDatosParaDonar()
    {
        TempData["Error"] = "Antes de publicar una causa, configura los datos para donar: es la cuenta donde los donantes harán sus aportes.";
        return RedirectToAction("Index", "DatosDonacion");
    }

    /// <summary>Avisa a los superadministradores que hay una causa por revisar.</summary>
    private async Task AvisarSuperadminsAsync(Causa causa, bool corregida)
    {
        var nombreEsal = await _db.Esales.Where(e => e.Id == causa.EsalId).Select(e => e.Nombre).FirstOrDefaultAsync() ?? "Una fundación";
        var titulo = corregida ? "Causa corregida para revisar" : "Nueva causa por aprobar";
        var mensaje = $"{nombreEsal} {(corregida ? "corrigió" : "propuso")} la causa \"{causa.Titulo}\" por {Formatos.Pesos(causa.Meta)}.";
        var url = $"/Plataforma/Causas/Revisar/{causa.Id}";

        var superadmins = await _usuarios.GetUsersInRoleAsync(Roles.SuperAdministrador);
        foreach (var admin in superadmins)
            _notificaciones.Agregar(admin.Id, titulo, mensaje, url, "bi-bullseye");
        await _db.SaveChangesAsync();

        foreach (var admin in superadmins.Where(a => !string.IsNullOrEmpty(a.Email)))
            await _avisos.EnviarAsync(admin.Email!, $"{titulo}: {causa.Titulo}", titulo, mensaje, "Revisar la causa", url);
    }

    private CausaFormViewModel Formulario(Causa c, decimal recaudado, CausaFormViewModel model)
    {
        model.Id = c.Id;
        model.Recaudado = recaudado;
        model.PideJustificacion = c.EnRevision;
        model.MotivoRechazo = c.MotivoRechazo;
        model.FotosActuales = c.Fotos.OrderBy(f => f.Orden).ThenBy(f => f.Id).Select(f => new FotoCausaItem(f.Id, _archivos.UrlPublica(f.Ruta))).ToList();
        return model;
    }

    private CausaItem Item(Causa c, decimal recaudado, DateTime hoy) => new()
    {
        Id = c.Id,
        Titulo = c.Titulo,
        Meta = c.Meta,
        Recaudado = recaudado,
        FechaLimite = c.FechaLimite,
        Estado = c.Estado,
        Cerrada = c.EstaCerrada(recaudado, hoy),
        Porcentaje = Formatos.Porcentaje(recaudado, c.Meta),
        DiasRestantes = c.DiasRestantes(hoy),
        FotoUrl = c.Fotos.OrderBy(f => f.Orden).ThenBy(f => f.Id).Select(f => _archivos.UrlPublica(f.Ruta)).FirstOrDefault(),
        MotivoRechazo = c.MotivoRechazo
    };

    private async Task<decimal> RecaudadoAsync(int causaId)
        => await _db.Donaciones.AsNoTracking()
            .Where(d => d.CausaId == causaId && d.Estado == EstadoDonacion.Confirmada)
            .SumAsync(d => (decimal?)d.Valor) ?? 0;

    private async Task<Dictionary<int, decimal>> RecaudadoPorCausaAsync()
        => await _db.Donaciones.AsNoTracking()
            .Where(d => d.CausaId != null && d.Estado == EstadoDonacion.Confirmada)
            .GroupBy(d => d.CausaId!.Value)
            .Select(g => new { g.Key, Total = g.Sum(x => x.Valor) })
            .ToDictionaryAsync(x => x.Key, x => x.Total);
}
