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

    /// <summary>
    /// El área va explícita en las redirecciones al listado: sin ella, Index se confunde con la página pública
    /// /causas, que está en un controlador con el mismo nombre.
    /// </summary>
    private const string AreaFundacion = "Fundacion";

    private readonly MunerAppDbContext _db;
    private readonly IEsalActual _esalActual;
    private readonly IAlmacenamientoArchivos _archivos;
    private readonly UserManager<Usuario> _usuarios;
    private readonly INotificacionService _notificaciones;
    private readonly AvisosCorreo _avisos;
    private readonly CierreCausas _cierre;
    private readonly RendicionCuentas _rendiciones;

    public CausasController(MunerAppDbContext db, IEsalActual esalActual, IAlmacenamientoArchivos archivos,
        UserManager<Usuario> usuarios, INotificacionService notificaciones, AvisosCorreo avisos,
        CierreCausas cierre, RendicionCuentas rendiciones)
    {
        _db = db;
        _esalActual = esalActual;
        _archivos = archivos;
        _usuarios = usuarios;
        _notificaciones = notificaciones;
        _avisos = avisos;
        _cierre = cierre;
        _rendiciones = rendiciones;
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
        var hoy = Formatos.Hoy();
        var conRendicion = (await _db.RendicionesCausa.AsNoTracking().Select(r => r.CausaId).ToListAsync()).ToHashSet();

        return View(new CausasIndexViewModel
        {
            Slug = await _db.Esales.AsNoTracking().Where(e => e.Id == EsalId).Select(e => e.Slug).FirstOrDefaultAsync(),
            PuedeGestionar = User.HasClaim(MunerAppClaims.Perfil, Perfiles.Principal),
            Causas = causas
                .Select(c => Item(c, recaudos.GetValueOrDefault(c.Id), hoy, conRendicion.Contains(c.Id)))
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
        return View(new CausaFormViewModel { FechaLimite = Formatos.Hoy().AddDays(30), PideJustificacion = true });
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
        return RedirectToAction(nameof(Index), new { area = AreaFundacion });
    }

    // ---------- Escenario 3: editar, pausar y reanudar ----------

    [HttpGet]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> Editar(int id)
    {
        var c = await _db.Causas.AsNoTracking().Include(x => x.Fotos).FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();

        var recaudado = await RecaudadoAsync(id);
        if (c.EstaCerrada(recaudado, Formatos.Hoy()))
        {
            TempData["Error"] = $"\"{c.Titulo}\" ya está cerrada y no se puede editar.";
            return RedirectToAction(nameof(Index), new { area = AreaFundacion });
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
        if (c.EstaCerrada(recaudado, Formatos.Hoy()))
        {
            TempData["Error"] = $"\"{c.Titulo}\" ya está cerrada y no se puede editar.";
            return RedirectToAction(nameof(Index), new { area = AreaFundacion });
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
        return RedirectToAction(nameof(Index), new { area = AreaFundacion });
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

        if (c.EstaCerrada(await RecaudadoAsync(id), Formatos.Hoy()))
        {
            TempData["Error"] = $"\"{c.Titulo}\" ya está cerrada.";
            return RedirectToAction(nameof(Index), new { area = AreaFundacion });
        }

        // Pausar o reanudar solo aplica a causas ya aprobadas
        if (c.EnRevision)
        {
            TempData["Error"] = $"\"{c.Titulo}\" todavía no está aprobada.";
            return RedirectToAction(nameof(Index), new { area = AreaFundacion });
        }

        var nuevo = pausar ? EstadoCausa.Pausada : EstadoCausa.Activa;
        if (c.Estado == nuevo)
        {
            TempData["Error"] = $"\"{c.Titulo}\" ya estaba {(pausar ? "en pausa" : "activa")}.";
            return RedirectToAction(nameof(Index), new { area = AreaFundacion });
        }

        c.Estado = nuevo;
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = pausar
            ? $"Pausaste \"{c.Titulo}\". Se sigue viendo, pero no recibe donaciones hasta que la reanudes."
            : $"Reanudaste \"{c.Titulo}\". Vuelve a recibir donaciones.";
        return RedirectToAction(nameof(Index), new { area = AreaFundacion });
    }

    // ---------- HU-046: cierre y rendición de cuentas ----------

    /// <summary>La fundación también puede cerrar una causa antes de tiempo (por ejemplo, si ya cubrió la necesidad).</summary>
    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> Cerrar(int id)
    {
        var c = await _db.Causas.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        if (c.EnRevision)
        {
            TempData["Error"] = $"\"{c.Titulo}\" todavía no está aprobada.";
            return RedirectToAction(nameof(Index), new { area = AreaFundacion });
        }
        if (c.Estado == EstadoCausa.Cerrada)
        {
            TempData["Error"] = $"\"{c.Titulo}\" ya estaba cerrada.";
            return RedirectToAction(nameof(Index), new { area = AreaFundacion });
        }

        var recaudado = await RecaudadoAsync(id);
        await _cierre.CerrarAsync(c, c.MotivoDeCierre(recaudado, Formatos.Local(DateTime.UtcNow).Date) ?? MotivoCierreCausa.Manual, recaudado, avisar: false);
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"Cerraste \"{c.Titulo}\" con {Formatos.Pesos(recaudado)} recaudados. Ya no recibe donaciones; ahora puedes publicar la rendición de cuentas.";
        return RedirectToAction(nameof(Rendicion), new { id });
    }

    /// <summary>Escenario 2: formulario de la rendición de cuentas de una causa cerrada.</summary>
    [HttpGet]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> Rendicion(int id)
    {
        var (c, error) = await CausaParaRendicionAsync(id);
        if (c is null) return error!;
        return View(await PrepararRendicionAsync(c, new RendicionFormViewModel()));
    }

    /// <summary>Escenarios 2 y 3: publica la rendición (causa, transparencia y boletín) y avisa a los donantes.</summary>
    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    [RequestSizeLimit(LimiteRendicion)]
    public async Task<IActionResult> Rendicion(int id, RendicionFormViewModel model)
    {
        var (c, error) = await CausaParaRendicionAsync(id, seguimiento: true);
        if (c is null) return error!;

        decimal? valor = null;
        if (!string.IsNullOrWhiteSpace(model.ValorEjecutado))
        {
            valor = Formatos.LeerPesos(model.ValorEjecutado);
            if (valor is null || valor < 0)
                ModelState.AddModelError(nameof(model.ValorEjecutado), "Escribe el valor solo con números, por ejemplo 480.000, o déjalo vacío.");
            else if (valor > FormularioCausa.MetaMaxima)
                ModelState.AddModelError(nameof(model.ValorEjecutado), "Revisa el valor.");
        }

        var archivos = new List<ArchivoParaRendicion>();
        await ValidarArchivosRendicionAsync(model.Fotos, nameof(model.Fotos), TipoArchivo.Imagen, RendicionCausa.MaxFotos, esSoporte: false, archivos);
        await ValidarArchivosRendicionAsync(model.Soportes, nameof(model.Soportes), TipoArchivo.Documento, RendicionCausa.MaxSoportes, esSoporte: true, archivos);
        if (ModelState.IsValid && archivos.Count == 0)
            ModelState.AddModelError(nameof(model.Fotos), "Agrega al menos una foto o un soporte (factura, recibo) de en qué se usó el dinero.");
        if (!ModelState.IsValid) return View(await PrepararRendicionAsync(c, model));

        int avisados;
        try
        {
            avisados = await _rendiciones.PublicarAsync(c, model.Texto, valor, archivos, UsuarioId);
        }
        catch (DbUpdateException)
        {
            // Otro administrador la publicó al mismo tiempo (la causa admite una sola rendición)
            _db.ChangeTracker.Clear();
            if (!await _db.RendicionesCausa.AsNoTracking().AnyAsync(r => r.CausaId == id)) throw;
            TempData["Error"] = $"La rendición de cuentas de \"{c.Titulo}\" ya fue publicada.";
            return RedirectToAction(nameof(Index), new { area = AreaFundacion });
        }

        TempData["Mensaje"] = $"Publicaste la rendición de cuentas de \"{c.Titulo}\": ya está en la causa, en transparencia y en el boletín. " +
                              (avisados == 0 ? "La causa no tenía donaciones confirmadas, así que no hubo a quién avisar."
                                  : avisados == 1 ? "Avisamos a la persona que donó." : $"Avisamos a las {avisados} personas que donaron.");
        return RedirectToAction(nameof(Index), new { area = AreaFundacion });
    }

    private const long LimiteRendicion = 52L * 1024 * 1024; // 5 fotos y 5 soportes de hasta 5 MB, y el texto

    /// <summary>La causa debe estar cerrada (guardada o porque ya cumplió la meta o el plazo) y sin rendición.</summary>
    private async Task<(Causa? Causa, IActionResult? Error)> CausaParaRendicionAsync(int id, bool seguimiento = false)
    {
        var consulta = seguimiento ? _db.Causas : _db.Causas.AsNoTracking();
        var c = await consulta.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return (null, NotFound());

        if (await _db.RendicionesCausa.AnyAsync(r => r.CausaId == id))
        {
            TempData["Error"] = $"La rendición de cuentas de \"{c.Titulo}\" ya fue publicada.";
            return (null, RedirectToAction(nameof(Index), new { area = AreaFundacion }));
        }
        if (!c.EstaCerrada(await RecaudadoAsync(id), Formatos.Local(DateTime.UtcNow).Date))
        {
            TempData["Error"] = $"\"{c.Titulo}\" sigue abierta. La rendición de cuentas se publica cuando la causa se cierra.";
            return (null, RedirectToAction(nameof(Index), new { area = AreaFundacion }));
        }
        return (c, null);
    }

    private async Task<RendicionFormViewModel> PrepararRendicionAsync(Causa c, RendicionFormViewModel model)
    {
        var confirmadas = _db.Donaciones.AsNoTracking().Where(d => d.CausaId == c.Id && d.Estado == EstadoDonacion.Confirmada);
        model.Id = c.Id;
        model.Titulo = c.Titulo;
        model.Meta = c.Meta;
        model.Recaudado = await confirmadas.SumAsync(d => (decimal?)d.Valor) ?? 0;
        model.Donaciones = await confirmadas.CountAsync();
        model.Donantes = await confirmadas.Select(d => d.DonanteId).Distinct().CountAsync();
        model.Motivo = CierreCausas.TextoMotivo(c, model.Recaudado, Formatos.Local(DateTime.UtcNow).Date);
        return model;
    }

    private async Task ValidarArchivosRendicionAsync(List<IFormFile>? lista, string campo, TipoArchivo tipo, int maximo,
        bool esSoporte, List<ArchivoParaRendicion> destino)
    {
        var archivos = (lista ?? new List<IFormFile>()).Where(f => f.Length > 0).ToList();
        if (archivos.Count > maximo)
        {
            ModelState.AddModelError(campo, $"Puedes adjuntar máximo {maximo}.");
            return;
        }
        foreach (var archivo in archivos)
        {
            var info = await ValidadorArchivos.ValidarAsync(archivo, tipo);
            if (!info.Valido)
                ModelState.AddModelError(campo, $"\"{Path.GetFileName(archivo.FileName)}\": {info.Error}");
            else
                destino.Add(new ArchivoParaRendicion(archivo, info, esSoporte));
        }
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

    private CausaItem Item(Causa c, decimal recaudado, DateTime hoy, bool tieneRendicion) => new()
    {
        TieneRendicion = tieneRendicion,
        MotivoCierre = c.EstaCerrada(recaudado, hoy) ? CierreCausas.TextoMotivo(c, recaudado, hoy) : null,
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
