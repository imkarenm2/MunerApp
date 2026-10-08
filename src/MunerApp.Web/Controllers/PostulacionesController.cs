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
using MunerApp.Web.Models.Publico;
using MunerApp.Web.Validacion;

namespace MunerApp.Web.Controllers;

/// <summary>HU-035: postulación como voluntario. La aprobación de postulaciones llega en el Sprint 6.</summary>
[Authorize]
public class PostulacionesController : Controller
{
    private readonly MunerAppDbContext _db;
    private readonly IAlmacenamientoArchivos _archivos;
    private readonly IModuloService _modulos;
    private readonly INotificacionService _notificaciones;
    private readonly IEsalActual _esalActual;

    public PostulacionesController(MunerAppDbContext db, IAlmacenamientoArchivos archivos, IModuloService modulos,
        INotificacionService notificaciones, IEsalActual esalActual)
    {
        _db = db;
        _archivos = archivos;
        _modulos = modulos;
        _notificaciones = notificaciones;
        _esalActual = esalActual;
    }

    private string UsuarioId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("fundaciones/{slug}/voluntariado")]
    public async Task<IActionResult> Crear(string slug)
    {
        var esal = await BuscarAsync(slug);
        if (esal is null) return NotFound();
        return View(await PrepararAsync(new PostulacionViewModel(), esal));
    }

    [HttpPost("fundaciones/{slug}/voluntariado")]
    [RequestSizeLimit(ValidadorArchivos.LimitePeticionBytes)]
    public async Task<IActionResult> Crear(string slug, PostulacionViewModel model)
    {
        var esal = await BuscarAsync(slug);
        if (esal is null) return NotFound();

        await PrepararAsync(model, esal);
        if (model.Aviso is not null) return View(model); // escenario 3: ya tiene una postulación en proceso

        model.Dias = model.Dias.Where(d => PostulacionViewModel.DiasSemana.Contains(d)).Distinct().ToList();
        if (model.Dias.Count == 0)
            ModelState.AddModelError(nameof(model.Dias), "Selecciona al menos un día en el que puedas ayudar.");
        if (!string.IsNullOrEmpty(model.Jornada) && !PostulacionViewModel.Jornadas.Contains(model.Jornada))
            ModelState.AddModelError(nameof(model.Jornada), "Selecciona una jornada de la lista.");

        ArchivoValidado? soporte = null;
        if (model.Tipo == TipoPostulacion.PracticanteSalud)
        {
            if (!model.PermitePracticante)
                ModelState.AddModelError(nameof(model.Tipo), "Esta fundación no recibe practicantes de salud por ahora.");

            // Escenario 2: el practicante debe indicar institución, programa y soporte académico
            if (string.IsNullOrWhiteSpace(model.Institucion))
                ModelState.AddModelError(nameof(model.Institucion), "Ingresa tu institución educativa.");
            if (string.IsNullOrWhiteSpace(model.Programa))
                ModelState.AddModelError(nameof(model.Programa), "Ingresa tu programa académico.");
            if (model.Semestre is null)
                ModelState.AddModelError(nameof(model.Semestre), "Ingresa el semestre que cursas.");
            if (model.SoporteAcademico is null || model.SoporteAcademico.Length == 0)
                ModelState.AddModelError(nameof(model.SoporteAcademico), "Adjunta tu certificado de estudio o la carta de tu institución.");
            else
            {
                soporte = await ValidadorArchivos.ValidarAsync(model.SoporteAcademico, TipoArchivo.Documento);
                if (!soporte.Valido) ModelState.AddModelError(nameof(model.SoporteAcademico), soporte.Error!);
            }
        }

        if (!ModelState.IsValid) return View(model);

        string? clave = null;
        if (soporte is not null)
        {
            await using var stream = model.SoporteAcademico!.OpenReadStream();
            clave = await _archivos.GuardarAsync(stream, $"esal/{esal.Id}/postulaciones", soporte.Extension, publico: false);
        }

        var practicante = model.Tipo == TipoPostulacion.PracticanteSalud;
        _db.PostulacionesVoluntario.Add(new PostulacionVoluntario
        {
            EsalId = esal.Id,
            UsuarioId = UsuarioId,
            Tipo = model.Tipo,
            Telefono = model.Telefono.Trim(),
            Disponibilidad = $"{string.Join(", ", model.Dias)} · {model.Jornada}",
            Motivacion = model.Motivacion?.Trim(),
            Institucion = practicante ? model.Institucion?.Trim() : null,
            Programa = practicante ? model.Programa?.Trim() : null,
            Semestre = practicante ? model.Semestre : null,
            SoporteAcademicoRuta = clave
        });

        await _notificaciones.AgregarAAdministradoresAsync(esal.Id,
            "Nueva postulación de voluntario",
            $"{User.FindFirstValue(MunerAppClaims.NombreCompleto) ?? "Una persona"} se postuló como {Textos.De(model.Tipo).ToLowerInvariant()}.",
            "/Fundacion/Postulaciones", "bi-people");
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"¡Listo! Enviaste tu postulación a {esal.Nombre}. Quedó pendiente y te avisaremos cuando la revisen.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("mis-postulaciones")]
    public async Task<IActionResult> Index()
    {
        var postulaciones = await _db.PostulacionesVoluntario.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.UsuarioId == UsuarioId)
            .OrderByDescending(p => p.FechaPostulacion)
            .Select(p => new { p.Esal!.Nombre, p.Esal.Slug, p.Esal.LogoRuta, p.Tipo, p.Estado, p.FechaPostulacion })
            .ToListAsync();

        return View(postulaciones.Select(p => new PostulacionItem
        {
            NombreEsal = p.Nombre,
            SlugEsal = p.Slug,
            LogoUrl = p.LogoRuta is null ? null : _archivos.UrlPublica(p.LogoRuta),
            Tipo = p.Tipo,
            Estado = p.Estado,
            Fecha = p.FechaPostulacion
        }).ToList());
    }

    private Task<Esal?> BuscarAsync(string slug)
        => _db.Esales.AsNoTracking().FirstOrDefaultAsync(e => e.Slug == slug && e.Activa);

    private async Task<PostulacionViewModel> PrepararAsync(PostulacionViewModel model, Esal esal)
    {
        model.Slug = esal.Slug!;
        model.NombreEsal = esal.Nombre;
        model.LogoUrl = esal.LogoRuta is null ? null : _archivos.UrlPublica(esal.LogoRuta);
        model.PermitePracticante = await _modulos.EstaActivoAsync(esal.Id, CodigosModulo.Salud);

        if (esal.VoluntariadoPausado)
            model.Aviso = $"{esal.Nombre} no está recibiendo voluntarios por ahora. Puedes apoyarla de otras formas.";
        else if (_esalActual.EsalId == esal.Id)
            model.Aviso = $"Ya haces parte del equipo de {esal.Nombre}.";
        else if (_esalActual.EsalId is not null || _esalActual.EsSuperAdmin)
            // Observación de pruebas Sprint 2: una cuenta de fundación no se postula como voluntaria de otra
            model.Aviso = "Las cuentas de una fundación no pueden postularse como voluntarias de otra. Si quieres ayudar a título personal, crea una cuenta con tu correo personal.";
        else if (await _db.PostulacionesVoluntario.IgnoreQueryFilters()
                     .AnyAsync(p => p.EsalId == esal.Id && p.UsuarioId == UsuarioId && p.Estado == EstadoPostulacion.Pendiente))
            model.Aviso = $"Ya tienes una postulación en proceso con {esal.Nombre}. Te avisaremos cuando la revisen.";

        return model;
    }
}
