using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MunerApp.Domain.Constantes;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Entities;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Areas.Fundacion.Models;
using MunerApp.Web.Seguridad;
using MunerApp.Web.Validacion;

namespace MunerApp.Web.Areas.Fundacion.Controllers;

/// <summary>HU-009: perfil institucional de la fundación (datos, logo y galería).</summary>
[Area("Fundacion")]
[Authorize(Policy = Politicas.AdminEsalPrincipal)]
public class PerfilController : Controller
{
    public const int MaxFotos = 8;

    private readonly MunerAppDbContext _db;
    private readonly IEsalActual _esalActual;
    private readonly IAlmacenamientoArchivos _archivos;
    private readonly ILogger<PerfilController> _logger;

    public PerfilController(MunerAppDbContext db, IEsalActual esalActual, IAlmacenamientoArchivos archivos, ILogger<PerfilController> logger)
    {
        _db = db;
        _esalActual = esalActual;
        _archivos = archivos;
        _logger = logger;
    }

    private int EsalId => _esalActual.EsalId ?? throw new InvalidOperationException("El usuario no pertenece a una ESAL.");

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var esal = await _db.Esales.AsNoTracking().FirstAsync(e => e.Id == EsalId);
        var modelo = new PerfilEsalViewModel
        {
            Nombre = esal.Nombre,
            Nit = esal.Nit,
            DescripcionCorta = esal.DescripcionCorta,
            Historia = esal.Historia,
            Mision = esal.Mision,
            Vision = esal.Vision,
            Ciudad = esal.Ciudad,
            Telefono = esal.Telefono,
            CorreoContacto = esal.CorreoContacto,
            VoluntariadoPausado = esal.VoluntariadoPausado
        };
        return View(await CompletarAsync(modelo, esal));
    }

    [HttpPost]
    [RequestSizeLimit(ValidadorArchivos.LimitePeticionBytes)]
    public async Task<IActionResult> Index(PerfilEsalViewModel model)
    {
        var esal = await _db.Esales.FirstAsync(e => e.Id == EsalId);

        model.Nombre = string.Join(' ', (model.Nombre ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        model.Nit = model.Nit?.Trim() ?? string.Empty;
        model.CorreoContacto = model.CorreoContacto?.Trim().ToLowerInvariant() ?? string.Empty;

        // No se puede repetir el NIT, el nombre ni el correo de otra fundación
        var otras = _db.Esales.AsNoTracking().Where(e => e.Id != esal.Id);
        if (!string.IsNullOrEmpty(model.Nit) && await otras.AnyAsync(e => e.Nit == model.Nit))
            ModelState.AddModelError(nameof(model.Nit), "Ya existe otra fundación registrada con este NIT.");
        if (!string.IsNullOrEmpty(model.Nombre) && await otras.AnyAsync(e => e.Nombre.ToLower() == model.Nombre.ToLower()))
            ModelState.AddModelError(nameof(model.Nombre), "Ya existe otra fundación registrada con este nombre.");
        if (!string.IsNullOrEmpty(model.CorreoContacto) && await otras.AnyAsync(e => e.CorreoContacto.ToLower() == model.CorreoContacto))
            ModelState.AddModelError(nameof(model.CorreoContacto), "Este correo ya es el contacto de otra fundación.");

        // Escenario 2: imagen inválida
        ArchivoValidado? logo = null;
        if (model.Logo is { Length: > 0 })
        {
            logo = await ValidadorArchivos.ValidarAsync(model.Logo, TipoArchivo.Imagen);
            if (!logo.Valido) ModelState.AddModelError(nameof(model.Logo), logo.Error!);
        }

        // Escenario 3: campos obligatorios (nombre, NIT y misión) los marca la validación del modelo
        if (!string.IsNullOrWhiteSpace(model.Ciudad) && !Region.EsMunicipioValido(model.Ciudad))
            ModelState.AddModelError(nameof(model.Ciudad), $"Por ahora MunerApp funciona solo en {Region.Nombre}. Selecciona un municipio de la lista.");
        if (!ModelState.IsValid) return View(await CompletarAsync(model, esal));

        esal.Nombre = model.Nombre;
        esal.Nit = model.Nit;
        esal.DescripcionCorta = Limpiar(model.DescripcionCorta);
        esal.Historia = Limpiar(model.Historia);
        esal.Mision = Limpiar(model.Mision);
        esal.Vision = Limpiar(model.Vision);
        esal.Ciudad = Limpiar(model.Ciudad);
        esal.Telefono = Limpiar(model.Telefono);
        esal.CorreoContacto = model.CorreoContacto;
        esal.VoluntariadoPausado = model.VoluntariadoPausado;
        esal.FechaActualizacionPerfil = DateTime.UtcNow;

        var logoAnterior = esal.LogoRuta;
        if (logo is { Valido: true })
        {
            try
            {
                await using var stream = model.Logo!.OpenReadStream();
                esal.LogoRuta = await _archivos.GuardarAsync(stream, $"esal/{esal.Id}/logo", logo.Extension, publico: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo guardar el logo de la ESAL {EsalId}", esal.Id);
                ModelState.AddModelError(nameof(model.Logo), "No pudimos guardar el logo. Intenta de nuevo con otra imagen.");
                return View(await CompletarAsync(model, esal));
            }
        }
        else if (model.QuitarLogo)
        {
            esal.LogoRuta = null;
        }

        await _db.SaveChangesAsync();
        if (logoAnterior is not null && logoAnterior != esal.LogoRuta)
            await _archivos.EliminarAsync(logoAnterior);

        // Escenario 1: el perfil público se actualiza con la nueva información
        TempData["Mensaje"] = "Guardaste el perfil. Así lo ven ahora los visitantes.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Galería ----------

    [HttpPost]
    [RequestSizeLimit(ValidadorArchivos.LimitePeticionBytes)]
    public async Task<IActionResult> SubirFoto(IFormFile? foto)
    {
        var cantidad = await _db.FotosEsal.CountAsync();
        if (cantidad >= MaxFotos)
        {
            TempData["Error"] = $"La galería admite máximo {MaxFotos} fotos. Elimina una para subir otra.";
            return RedirectToAction(nameof(Index), null, "galeria");
        }

        var validacion = await ValidadorArchivos.ValidarAsync(foto, TipoArchivo.Imagen);
        if (!validacion.Valido)
        {
            TempData["Error"] = $"No se subió la foto. {validacion.Error}";
            return RedirectToAction(nameof(Index), null, "galeria");
        }

        try
        {
            await using var stream = foto!.OpenReadStream();
            var clave = await _archivos.GuardarAsync(stream, $"esal/{EsalId}/galeria", validacion.Extension, publico: true);
            _db.FotosEsal.Add(new FotoEsal { EsalId = EsalId, Ruta = clave, Orden = cantidad + 1 });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo guardar una foto de la ESAL {EsalId}", EsalId);
            TempData["Error"] = "No pudimos guardar la foto. Intenta de nuevo con otra imagen.";
            return RedirectToAction(nameof(Index), null, "galeria");
        }
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = "Agregaste la foto a la galería.";
        return RedirectToAction(nameof(Index), null, "galeria");
    }

    [HttpPost]
    public async Task<IActionResult> EliminarFoto(int id)
    {
        // El filtro por ESAL impide borrar fotos de otra fundación
        var foto = await _db.FotosEsal.FirstOrDefaultAsync(f => f.Id == id);
        if (foto is null) return NotFound();

        _db.FotosEsal.Remove(foto);
        await _db.SaveChangesAsync();
        await _archivos.EliminarAsync(foto.Ruta);

        TempData["Mensaje"] = "Eliminaste la foto de la galería.";
        return RedirectToAction(nameof(Index), null, "galeria");
    }

    private async Task<PerfilEsalViewModel> CompletarAsync(PerfilEsalViewModel model, Esal esal)
    {
        model.Slug = esal.Slug;
        model.LogoUrl = esal.LogoRuta is null ? null : _archivos.UrlPublica(esal.LogoRuta);
        model.FechaActualizacion = esal.FechaActualizacionPerfil;
        model.Fotos = (await _db.FotosEsal.AsNoTracking().OrderBy(f => f.Orden).ThenBy(f => f.Id).ToListAsync())
            .Select(f => new FotoItem(f.Id, _archivos.UrlPublica(f.Ruta))).ToList();
        return model;
    }

    private static string? Limpiar(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
