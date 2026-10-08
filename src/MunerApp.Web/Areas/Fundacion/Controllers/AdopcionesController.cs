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
    private readonly EstadosBeneficiario _estados;

    public AdopcionesController(MunerAppDbContext db, IEsalActual esalActual, IAlmacenamientoArchivos archivos,
        INotificacionService notificaciones, EstadosBeneficiario estados)
    {
        _db = db;
        _esalActual = esalActual;
        _archivos = archivos;
        _notificaciones = notificaciones;
        _estados = estados;
    }

    private int EsalId => _esalActual.EsalId ?? throw new InvalidOperationException("El usuario no pertenece a una ESAL.");

    // ---------- HU-033: panel de solicitudes ----------

    /// <summary>Escenario 1: solicitudes con fecha, nombre y estado. Los borradores no se muestran: aún no se han enviado.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(EstadoSolicitudAdopcion estado = EstadoSolicitudAdopcion.Recibida)
    {
        // La pestaña que contiene el estado pedido (por ejemplo, "Cerradas" agrupa rechazadas y no concretadas)
        var pestana = SolicitudesAdopcionViewModel.Pestanas.FirstOrDefault(p => p.Estados.Contains(estado))
                      ?? SolicitudesAdopcionViewModel.Pestanas[0];
        var estados = pestana.Estados;

        var conteos = await _db.SolicitudesAdopcion.AsNoTracking()
            .Where(s => s.Estado != EstadoSolicitudAdopcion.Borrador)
            .GroupBy(s => s.Estado)
            .Select(g => new { g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Cantidad);

        var consulta = _db.SolicitudesAdopcion.AsNoTracking().Where(s => estados.Contains(s.Estado));
        // Las citas, por fecha de la cita; lo demás, las más antiguas primero (llevan más tiempo esperando)
        consulta = estado == EstadoSolicitudAdopcion.CitaAgendada
            ? consulta.OrderBy(s => s.FechaCita)
            : consulta.OrderBy(s => s.FechaEnvio);

        var solicitudes = await consulta
            .Select(s => new SolicitudAdopcionEsalItem
            {
                Id = s.Id,
                Codigo = s.Codigo!,
                Nombre = s.NombreCompleto!,
                Ciudad = s.Ciudad!,
                FechaEnvio = s.FechaEnvio!.Value,
                Estado = s.Estado,
                FechaCita = s.FechaCita
            }).Take(200).ToListAsync();

        return View(new SolicitudesAdopcionViewModel
        {
            Estado = pestana.Estados[0],
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

        var modelo = new SolicitudAdopcionDetalleViewModel
        {
            Solicitud = s,
            Correo = correo ?? "",
            RevisadoPor = revisor,
            CarneEsPdf = s.CarneVacunasRuta?.EndsWith(".pdf") == true,
            ValorAporte = await ValorAporteAsync(),
            NombreBeneficiarioAdoptado = s.BeneficiarioAdoptadoId is null ? null
                : await _db.Beneficiarios.AsNoTracking().Where(b => b.Id == s.BeneficiarioAdoptadoId).Select(b => b.Nombre).FirstOrDefaultAsync()
        };
        if (s.Estado == EstadoSolicitudAdopcion.CitaAgendada)
        {
            modelo.BeneficiariosDisponibles = await _db.Beneficiarios.AsNoTracking()
                .Where(b => b.Estado != EstadoBeneficiario.Adoptado && b.Estado != EstadoBeneficiario.Fallecido)
                .OrderBy(b => b.Nombre)
                .Select(b => new BeneficiarioOpcion(b.Id, b.Nombre, b.Estado))
                .ToListAsync();
        }
        return View(modelo);
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

    // ---------- HU-034: cita presencial y resultado ----------

    /// <summary>Escenario 1: agendar la cita de una solicitud aprobada. Escenario 2: reprogramarla si ya estaba agendada.</summary>
    [HttpPost]
    public async Task<IActionResult> AgendarCita(int id, CitaAdopcionViewModel model)
    {
        var s = await BuscarEnviadaAsync(id, seguimiento: true);
        if (s is null) return NotFound();

        var reprogramando = s.Estado == EstadoSolicitudAdopcion.CitaAgendada;
        if (!reprogramando && s.Estado != EstadoSolicitudAdopcion.AprobadaParaCita)
        {
            TempData["Error"] = "Solo se agenda la cita de una solicitud aprobada.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        var lugar = model.Lugar?.Trim();
        var indicaciones = string.IsNullOrWhiteSpace(model.Indicaciones) ? null : model.Indicaciones.Trim();
        string? error = null;
        if (model.Fecha is null) error = "Selecciona la fecha y la hora de la cita.";
        else if (Formatos.Utc(model.Fecha.Value) <= DateTime.UtcNow) error = "La cita debe ser en una fecha y hora futuras.";
        else if (Formatos.Utc(model.Fecha.Value) > DateTime.UtcNow.AddMonths(6)) error = "La cita debe ser en los próximos 6 meses.";
        else if (string.IsNullOrEmpty(lugar) || lugar.Length < 5) error = "Escribe el lugar de la cita (por ejemplo, la dirección de la fundación).";
        else if (lugar.Length > 200 || indicaciones?.Length > 300) error = "El lugar admite hasta 200 caracteres y las indicaciones hasta 300.";
        if (error is not null)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Detalle), new { id });
        }

        var fechaUtc = Formatos.Utc(model.Fecha!.Value);
        var cambioFecha = reprogramando && s.FechaCita != fechaUtc;
        if (reprogramando && !cambioFecha && s.LugarCita == lugar && s.IndicacionesCita == indicaciones)
        {
            TempData["Error"] = "No cambiaste ningún dato de la cita.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        s.FechaCita = fechaUtc;
        s.LugarCita = lugar;
        s.IndicacionesCita = indicaciones;
        s.Estado = EstadoSolicitudAdopcion.CitaAgendada;
        s.FechaActualizacion = DateTime.UtcNow;
        if (cambioFecha) s.Reprogramaciones++;

        // La persona recibe la cita con los requisitos: aporte, huacal y documento
        var cita = $"{Formatos.FechaHora(fechaUtc)} en {lugar}";
        var requisitos = Requisitos(await ValorAporteAsync());
        if (!reprogramando)
            _notificaciones.Agregar(s.UsuarioId, "Tienes cita para tu adopción",
                $"{s.Esal!.Nombre} te espera el {cita}. {requisitos}", "/mis-adopciones", "bi-calendar-check");
        else
            _notificaciones.Agregar(s.UsuarioId, cambioFecha ? "Cambió la fecha de tu cita de adopción" : "Se actualizaron los datos de tu cita",
                $"{s.Esal!.Nombre}: tu cita ahora es el {cita}. {requisitos}", "/mis-adopciones", "bi-calendar-event");
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = reprogramando
            ? $"Actualizaste la cita de {s.NombreCompleto}. Le avisamos el cambio."
            : $"Agendaste la cita de {s.NombreCompleto} para el {cita}. Le avisamos con los requisitos.";
        return RedirectToAction(nameof(Detalle), new { id });
    }

    /// <summary>Escenario 3: resultado de la cita. Si se concreta, el beneficiario pasa a "Adoptado" con los datos del adoptante.</summary>
    [HttpPost]
    public async Task<IActionResult> RegistrarResultado(int id, ResultadoCitaViewModel model)
    {
        var s = await BuscarEnviadaAsync(id, seguimiento: true);
        if (s is null) return NotFound();
        if (s.Estado != EstadoSolicitudAdopcion.CitaAgendada)
        {
            TempData["Error"] = "El resultado se registra para una solicitud con cita agendada.";
            return RedirectToAction(nameof(Detalle), new { id });
        }
        if (Formatos.Local(s.FechaCita!.Value).Date > Formatos.Local(DateTime.UtcNow).Date)
        {
            TempData["Error"] = $"La cita es el {Formatos.FechaHora(s.FechaCita.Value)}: registra el resultado cuando se haya realizado.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        var observaciones = string.IsNullOrWhiteSpace(model.Observaciones) ? null : model.Observaciones.Trim();
        if (observaciones?.Length > 500) observaciones = observaciones[..500];

        if (model.Concretada is null)
        {
            TempData["Error"] = "Indica si la adopción se concretó.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        if (model.Concretada == false)
        {
            if (observaciones is null)
            {
                TempData["Error"] = "Cuéntanos por qué no se concretó la adopción (queda solo para la fundación).";
                return RedirectToAction(nameof(Detalle), new { id });
            }
            s.Estado = EstadoSolicitudAdopcion.NoConcretada;
            s.ObservacionesResultado = observaciones;
            s.FechaResultado = DateTime.UtcNow;
            s.FechaActualizacion = DateTime.UtcNow;
            _notificaciones.Agregar(s.UsuarioId, "Tu proceso de adopción terminó",
                $"Gracias por acercarte a {s.Esal!.Nombre}. Esta vez la adopción no se concretó; puedes volver a solicitar cuando quieras.",
                "/mis-adopciones", "bi-info-circle");
            await _db.SaveChangesAsync();

            TempData["Mensaje"] = $"Registraste que la adopción de {s.NombreCompleto} no se concretó.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        // Se concretó: el beneficiario debe ser de la fundación (filtro por ESAL) y poder adoptarse
        var b = model.BeneficiarioId is null ? null
            : await _db.Beneficiarios.Include(x => x.Adoptante).FirstOrDefaultAsync(x => x.Id == model.BeneficiarioId);
        if (b is null)
        {
            TempData["Error"] = "Selecciona el beneficiario que se llevó la persona.";
            return RedirectToAction(nameof(Detalle), new { id });
        }
        if (b.Estado is EstadoBeneficiario.Adoptado or EstadoBeneficiario.Fallecido)
        {
            TempData["Error"] = $"{b.Nombre} está en estado \"{Textos.De(b.Estado)}\" y no puede adoptarse.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _estados.CambiarAsync(b, EstadoBeneficiario.Adoptado, usuarioId, $"Adoptado por {s.NombreCompleto} (solicitud {s.Codigo}).");

        // Datos del adoptante para el seguimiento posterior (HU-017, escenario 3), tomados de la solicitud
        var a = b.Adoptante;
        if (a is null)
        {
            a = new AdoptanteBeneficiario { BeneficiarioId = b.Id, EsalId = b.EsalId };
            _db.AdoptantesBeneficiario.Add(a);
        }
        a.Nombre = s.NombreCompleto!;
        a.Documento = s.Cedula!;
        a.Telefono = s.Celular!;
        a.Correo = await _db.Users.Where(u => u.Id == s.UsuarioId).Select(u => u.Email).FirstOrDefaultAsync();
        a.Ciudad = s.Ciudad!;
        a.Direccion = s.Direccion!;
        a.FechaAdopcion = Formatos.Local(DateTime.UtcNow).Date;
        a.Observaciones = $"Solicitud de adopción {s.Codigo}." + (observaciones is null ? "" : " " + observaciones);
        if (a.Observaciones.Length > 500) a.Observaciones = a.Observaciones[..500];
        a.RegistradoPorId = usuarioId;

        s.Estado = EstadoSolicitudAdopcion.AdopcionConcretada;
        s.BeneficiarioAdoptadoId = b.Id;
        s.ObservacionesResultado = observaciones;
        s.FechaResultado = DateTime.UtcNow;
        s.FechaActualizacion = DateTime.UtcNow;
        _notificaciones.Agregar(s.UsuarioId, $"¡Felicitaciones! {b.Nombre} ya es parte de tu familia",
            $"{s.Esal!.Nombre} registró la adopción de {b.Nombre}. Gracias por darle un hogar.", "/mis-adopciones", "bi-heart-fill");
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"¡Adopción concretada! {b.Nombre} quedó en estado \"Adoptado\" con los datos de {s.NombreCompleto}.";
        return RedirectToAction(nameof(Detalle), new { id });
    }

    /// <summary>Lo que la persona debe llevar a la cita (HU-034, escenario 1).</summary>
    private static string Requisitos(decimal valorAporte)
        => $"Lleva {Formatos.Pesos(valorAporte)} para la esterilización y la vacuna, un huacal para transportarlo y tu documento de identidad.";

    private async Task<decimal> ValorAporteAsync()
        => await _db.ConfigAdopciones.AsNoTracking().Select(c => (decimal?)c.ValorAporte).FirstOrDefaultAsync()
           ?? ConfigAdopcion.ValorAportePredeterminado;

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
