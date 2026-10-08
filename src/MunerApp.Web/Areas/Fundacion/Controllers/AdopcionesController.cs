using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Application.Seguridad;
using MunerApp.Domain.Constantes;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Areas.Fundacion.Models;
using MunerApp.Web.Filtros;
using MunerApp.Web.Seguridad;
using MunerApp.Web.Servicios;
using MunerApp.Web.Validacion;

namespace MunerApp.Web.Areas.Fundacion.Controllers;

/// <summary>
/// Módulo de adopción de la fundación.
/// HU-033: los administradores (principal y de consulta) revisan las solicitudes y las aprueban para cita o las rechazan.
/// Configuración del proceso, solo el principal: recomendaciones (HU-029), aporte e información de toxoplasmosis (HU-032).
/// El filtro global por ESAL garantiza que solo vean las solicitudes de su fundación.
/// </summary>
[Area("Fundacion")]
[Authorize(Roles = Roles.AdministradorESAL)]
[RequiereModulo(CodigosModulo.Adopcion)]
public class AdopcionesController : Controller
{
    private const decimal AporteMaximo = 10_000_000;

    private readonly MunerAppDbContext _db;
    private readonly IEsalActual _esalActual;
    private readonly IAlmacenamientoArchivos _archivos;
    private readonly INotificacionService _notificaciones;

    public AdopcionesController(MunerAppDbContext db, IEsalActual esalActual, IAlmacenamientoArchivos archivos,
        INotificacionService notificaciones)
    {
        _db = db;
        _esalActual = esalActual;
        _archivos = archivos;
        _notificaciones = notificaciones;
    }

    private int EsalId => _esalActual.EsalId ?? throw new InvalidOperationException("El usuario no pertenece a una ESAL.");

    // ---------- HU-033: panel de solicitudes ----------

    /// <summary>Escenario 1: solicitudes con fecha, nombre y estado. Los borradores no se muestran: aún no se han enviado.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(EstadoSolicitudAdopcion estado = EstadoSolicitudAdopcion.Recibida)
    {
        if (estado == EstadoSolicitudAdopcion.Borrador) estado = EstadoSolicitudAdopcion.Recibida;

        var conteos = await _db.SolicitudesAdopcion.AsNoTracking()
            .Where(s => s.Estado != EstadoSolicitudAdopcion.Borrador)
            .GroupBy(s => s.Estado)
            .Select(g => new { g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Cantidad);

        var solicitudes = await _db.SolicitudesAdopcion.AsNoTracking()
            .Where(s => s.Estado == estado)
            .OrderBy(s => s.FechaEnvio) // las más antiguas primero: llevan más tiempo esperando
            .Select(s => new SolicitudAdopcionEsalItem
            {
                Id = s.Id,
                Codigo = s.Codigo!,
                Nombre = s.NombreCompleto!,
                Ciudad = s.Ciudad!,
                FechaEnvio = s.FechaEnvio!.Value,
                Estado = s.Estado
            }).Take(200).ToListAsync();

        return View(new SolicitudesAdopcionViewModel
        {
            Estado = estado,
            Conteos = conteos,
            Solicitudes = solicitudes,
            PuedeConfigurar = User.HasClaim(MunerAppClaims.Perfil, Perfiles.Principal)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Detalle(int id)
    {
        var s = await BuscarEnviadaAsync(id, seguimiento: false);
        if (s is null) return NotFound();

        var correo = await _db.Users.AsNoTracking().Where(u => u.Id == s.UsuarioId).Select(u => u.Email).FirstOrDefaultAsync();
        var revisor = s.RevisadoPorId is null ? null
            : await _db.Users.AsNoTracking().Where(u => u.Id == s.RevisadoPorId).Select(u => u.NombreCompleto).FirstOrDefaultAsync();

        return View(new SolicitudAdopcionDetalleViewModel
        {
            Solicitud = s,
            Correo = correo ?? "",
            RevisadoPor = revisor,
            CarneEsPdf = s.CarneVacunasRuta?.EndsWith(".pdf") == true
        });
    }

    /// <summary>Carné de vacunas que adjuntó el solicitante (archivo privado).</summary>
    [HttpGet]
    public async Task<IActionResult> Carne(int id)
    {
        var s = await BuscarEnviadaAsync(id, seguimiento: false);
        if (s?.CarneVacunasRuta is null) return NotFound();
        var stream = await _archivos.AbrirAsync(s.CarneVacunasRuta);
        return stream is null ? NotFound() : File(stream, ValidadorArchivos.ContentTypeDe(s.CarneVacunasRuta));
    }

    // Escenario 2: aprobar para cita
    [HttpPost]
    public async Task<IActionResult> Aprobar(int id)
    {
        var s = await BuscarEnviadaAsync(id, seguimiento: true);
        if (s is null) return NotFound();
        if (s.Estado != EstadoSolicitudAdopcion.Recibida)
        {
            TempData["Error"] = $"La solicitud {s.Codigo} ya había sido revisada.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        s.Estado = EstadoSolicitudAdopcion.AprobadaParaCita;
        MarcarRevisada(s);

        _notificaciones.Agregar(s.UsuarioId, "¡Tu solicitud de adopción fue aprobada!",
            $"{s.Esal!.Nombre} aprobó tu solicitud {s.Codigo}. Pronto te asignarán la cita presencial para conocer a los gatos.",
            "/mis-adopciones", "bi-house-check");
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"Aprobaste la solicitud {s.Codigo}. El siguiente paso es agendar la cita presencial.";
        return RedirectToAction(nameof(Index), new { estado = EstadoSolicitudAdopcion.AprobadaParaCita });
    }

    // Escenario 3: rechazar con motivo
    [HttpPost]
    public async Task<IActionResult> Rechazar(int id, string? motivo)
    {
        var s = await BuscarEnviadaAsync(id, seguimiento: true);
        if (s is null) return NotFound();
        if (s.Estado != EstadoSolicitudAdopcion.Recibida)
        {
            TempData["Error"] = $"La solicitud {s.Codigo} ya había sido revisada.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        motivo = motivo?.Trim();
        if (string.IsNullOrEmpty(motivo) || motivo.Length < 10)
        {
            TempData["Error"] = "Escribe el motivo del rechazo (mínimo 10 caracteres) para que la persona sepa por qué.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        s.Estado = EstadoSolicitudAdopcion.Rechazada;
        s.MotivoRechazo = motivo.Length > 300 ? motivo[..300] : motivo;
        MarcarRevisada(s);

        _notificaciones.Agregar(s.UsuarioId, "Tu solicitud de adopción no fue aprobada",
            $"{s.Esal!.Nombre} no aprobó tu solicitud {s.Codigo}. Motivo: {s.MotivoRechazo}",
            "/mis-adopciones", "bi-x-circle");
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"Rechazaste la solicitud {s.Codigo}. La persona verá el motivo.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Solicitud enviada de la fundación (nunca un borrador), o null.</summary>
    private Task<SolicitudAdopcion?> BuscarEnviadaAsync(int id, bool seguimiento)
    {
        var consulta = _db.SolicitudesAdopcion.Include(s => s.Esal).Where(s => s.Id == id && s.Estado != EstadoSolicitudAdopcion.Borrador);
        return (seguimiento ? consulta : consulta.AsNoTracking()).FirstOrDefaultAsync();
    }

    private void MarcarRevisada(SolicitudAdopcion s)
    {
        s.FechaRevision = DateTime.UtcNow;
        s.FechaActualizacion = DateTime.UtcNow;
        s.RevisadoPorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    // ---------- Configuración del proceso (HU-029, HU-032) ----------

    [HttpGet]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> Configuracion()
    {
        var config = await _db.ConfigAdopciones.AsNoTracking().FirstOrDefaultAsync();
        var model = new ConfigAdopcionViewModel
        {
            Recomendaciones = config?.Recomendaciones ?? ConfigAdopcion.RecomendacionesPredeterminadas,
            ValorAporte = ((long)(config?.ValorAporte ?? ConfigAdopcion.ValorAportePredeterminado)).ToString("N0", new System.Globalization.CultureInfo("es-CO")),
            MensajeToxoplasmosis = config?.MensajeToxoplasmosis ?? ConfigAdopcion.MensajeToxoplasmosisPredeterminado
        };
        return View(await PrepararAsync(model, config));
    }

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    [RequestSizeLimit(ValidadorArchivos.LimitePeticionBytes)]
    public async Task<IActionResult> Configuracion(ConfigAdopcionViewModel model)
    {
        var config = await _db.ConfigAdopciones.FirstOrDefaultAsync();

        // HU-029: una recomendación por línea, sin líneas vacías
        var lineas = ConfigAdopcion.ComoLista(model.Recomendaciones ?? "");
        if (string.IsNullOrWhiteSpace(model.Recomendaciones) || lineas.Count == 0)
            ModelState.AddModelError(nameof(model.Recomendaciones), "Escribe al menos una recomendación.");

        // HU-032: valor del aporte y la imagen de toxoplasmosis
        var aporte = Formatos.LeerPesos(model.ValorAporte);
        if (!string.IsNullOrWhiteSpace(model.ValorAporte))
        {
            if (aporte is null)
                ModelState.AddModelError(nameof(model.ValorAporte), "Escribe el valor solo con números, por ejemplo 100.000.");
            else if (aporte > AporteMaximo)
                ModelState.AddModelError(nameof(model.ValorAporte), $"El valor no puede superar {Formatos.Pesos(AporteMaximo)}.");
        }

        ArchivoValidado? imagen = null;
        if (model.ImagenToxoplasmosis is { Length: > 0 })
        {
            imagen = await ValidadorArchivos.ValidarAsync(model.ImagenToxoplasmosis, TipoArchivo.Imagen);
            if (!imagen.Valido) ModelState.AddModelError(nameof(model.ImagenToxoplasmosis), imagen.Error!);
        }

        if (!ModelState.IsValid) return View(await PrepararAsync(model, config));

        if (config is null)
        {
            config = new ConfigAdopcion { EsalId = EsalId };
            _db.ConfigAdopciones.Add(config);
        }

        if (config.ImagenToxoplasmosisRuta is not null && (imagen is not null || model.QuitarImagen))
        {
            await _archivos.EliminarAsync(config.ImagenToxoplasmosisRuta);
            config.ImagenToxoplasmosisRuta = null;
        }
        if (imagen is not null)
        {
            await using var stream = model.ImagenToxoplasmosis!.OpenReadStream();
            config.ImagenToxoplasmosisRuta = await _archivos.GuardarAsync(stream, $"esal/{EsalId}/adopcion", imagen.Extension, publico: true);
        }

        config.Recomendaciones = string.Join("\n", lineas);
        config.ValorAporte = aporte!.Value;
        config.MensajeToxoplasmosis = string.IsNullOrWhiteSpace(model.MensajeToxoplasmosis) ? null : model.MensajeToxoplasmosis.Trim();
        config.FechaActualizacion = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = "Guardaste la configuración de adopción. Las personas la verán al solicitar una adopción.";
        return RedirectToAction(nameof(Configuracion));
    }

    private async Task<ConfigAdopcionViewModel> PrepararAsync(ConfigAdopcionViewModel model, ConfigAdopcion? config)
    {
        model.Personalizadas = config is not null;
        model.FechaActualizacion = config?.FechaActualizacion;
        model.ImagenToxoplasmosisUrl = config?.ImagenToxoplasmosisRuta is null ? null : _archivos.UrlPublica(config.ImagenToxoplasmosisRuta);
        model.Slug = await _db.Esales.Where(e => e.Id == EsalId).Select(e => e.Slug).FirstOrDefaultAsync();
        return model;
    }
}
