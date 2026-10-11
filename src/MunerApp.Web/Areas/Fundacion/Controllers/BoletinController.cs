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
/// HU-027: publicaciones del boletín (noticias, eventos, logros y rendiciones de cuentas).
/// Escenario 1: se crean como borrador, que solo ve el equipo de la fundación.
/// Escenario 2: al publicarlas aparecen en el boletín público (HU-028); se pueden volver a borrador.
/// Escenario 3: un evento exige fecha futura y lugar. Una rendición de cuentas puede ligarse a una causa.
/// Los administradores ven el listado; crear, editar, publicar y eliminar es del administrador principal.
/// El módulo Boletín es general (siempre activo).
/// </summary>
[Area("Fundacion")]
[Authorize(Roles = Roles.AdministradorESAL)]
[RequiereModulo(CodigosModulo.Boletin)]
public class BoletinController : Controller
{
    private const long LimitePeticion = 8L * 1024 * 1024; // una imagen de hasta 5 MB y el texto

    private readonly MunerAppDbContext _db;
    private readonly IEsalActual _esalActual;
    private readonly IAlmacenamientoArchivos _archivos;

    public BoletinController(MunerAppDbContext db, IEsalActual esalActual, IAlmacenamientoArchivos archivos)
    {
        _db = db;
        _esalActual = esalActual;
        _archivos = archivos;
    }

    private string UsuarioId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private int EsalId => _esalActual.EsalId ?? throw new InvalidOperationException("El usuario no pertenece a una ESAL.");

    // ---------- Listado ----------

    [HttpGet]
    public async Task<IActionResult> Index(EstadoPublicacion estado = EstadoPublicacion.Publicada)
    {
        var lista = await _db.Publicaciones.AsNoTracking()
            .Where(p => p.Estado == estado)
            .OrderByDescending(p => p.FechaPublicacion ?? p.FechaCreacion)
            .ToListAsync();
        var conteos = await _db.Publicaciones.GroupBy(p => p.Estado).Select(g => new { g.Key, Total = g.Count() }).ToListAsync();

        return View(new BoletinIndexViewModel
        {
            Estado = estado,
            Borradores = conteos.FirstOrDefault(c => c.Key == EstadoPublicacion.Borrador)?.Total ?? 0,
            Publicadas = conteos.FirstOrDefault(c => c.Key == EstadoPublicacion.Publicada)?.Total ?? 0,
            Slug = await _db.Esales.AsNoTracking().Where(e => e.Id == EsalId).Select(e => e.Slug).FirstOrDefaultAsync(),
            PuedeGestionar = User.HasClaim(MunerAppClaims.Perfil, Perfiles.Principal),
            Publicaciones = lista.Select(p => new PublicacionItem
            {
                Id = p.Id,
                Titulo = p.Titulo,
                Categoria = p.Categoria,
                Estado = p.Estado,
                ImagenUrl = p.ImagenRuta is null ? null : _archivos.UrlPublica(p.ImagenRuta),
                FechaCreacion = p.FechaCreacion,
                FechaPublicacion = p.FechaPublicacion,
                FechaEvento = p.FechaEvento
            }).ToList()
        });
    }

    // ---------- Escenario 1: crear (borrador o publicar de una vez) ----------

    [HttpGet]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> Crear(CategoriaPublicacion? categoria, int? causa)
        => View(await PrepararAsync(new PublicacionFormViewModel
        {
            Categoria = categoria ?? CategoriaPublicacion.Noticia,
            CausaId = causa
        }, null));

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    [RequestSizeLimit(LimitePeticion)]
    public async Task<IActionResult> Crear(PublicacionFormViewModel model, bool publicar)
    {
        await ValidarAsync(model, nueva: true);
        var imagen = await ValidarImagenAsync(model);
        if (!ModelState.IsValid) return View(await PrepararAsync(model, null));

        var p = new Publicacion { EsalId = EsalId, CreadaPorId = UsuarioId };
        Copiar(model, p);
        if (imagen is not null) p.ImagenRuta = await GuardarImagenAsync(model.Imagen!, imagen);
        if (publicar)
        {
            p.Estado = EstadoPublicacion.Publicada;
            p.FechaPublicacion = DateTime.UtcNow;
        }
        _db.Publicaciones.Add(p);
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = publicar
            ? $"Publicaste \"{p.Titulo}\". Ya aparece en el boletín de la fundación."
            : $"Guardaste \"{p.Titulo}\" como borrador. Solo lo ve el equipo hasta que lo publiques.";
        return RedirectToAction(nameof(Index), new { estado = p.Estado });
    }

    // ---------- Editar ----------

    [HttpGet]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> Editar(int id)
    {
        var p = await _db.Publicaciones.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();

        return View(await PrepararAsync(new PublicacionFormViewModel
        {
            Titulo = p.Titulo,
            Categoria = p.Categoria,
            Resumen = p.Resumen,
            Contenido = p.Contenido,
            FechaEvento = p.FechaEvento is DateTime f ? Formatos.Local(f) : null,
            LugarEvento = p.LugarEvento,
            CausaId = p.CausaId
        }, p));
    }

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    [RequestSizeLimit(LimitePeticion)]
    public async Task<IActionResult> Editar(int id, PublicacionFormViewModel model)
    {
        var p = await _db.Publicaciones.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();

        // Un evento ya publicado puede quedar con fecha pasada; solo se exige fecha futura al cambiarla
        var fechaOriginal = p.FechaEvento is DateTime f ? Formatos.Local(f) : (DateTime?)null;
        await ValidarAsync(model, nueva: model.FechaEvento != fechaOriginal);
        var imagen = await ValidarImagenAsync(model);
        if (!ModelState.IsValid) return View(await PrepararAsync(model, p));

        Copiar(model, p);
        p.FechaActualizacion = DateTime.UtcNow;
        string? anterior = null;
        if (imagen is not null)
        {
            anterior = p.ImagenRuta;
            p.ImagenRuta = await GuardarImagenAsync(model.Imagen!, imagen);
        }
        await _db.SaveChangesAsync();
        if (anterior is not null) await _archivos.EliminarAsync(anterior);

        TempData["Mensaje"] = $"Guardaste los cambios de \"{p.Titulo}\".";
        return RedirectToAction(nameof(Index), new { estado = p.Estado });
    }

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> QuitarImagen(int id)
    {
        var p = await _db.Publicaciones.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();

        var ruta = p.ImagenRuta;
        p.ImagenRuta = null;
        await _db.SaveChangesAsync();
        if (ruta is not null) await _archivos.EliminarAsync(ruta);

        TempData["Mensaje"] = "Quitaste la imagen.";
        return RedirectToAction(nameof(Editar), new { id });
    }

    // ---------- Escenario 2: publicar y volver a borrador ----------

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> Publicar(int id)
    {
        var p = await _db.Publicaciones.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();

        p.Estado = EstadoPublicacion.Publicada;
        p.FechaPublicacion ??= DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"Publicaste \"{p.Titulo}\". Ya aparece en el boletín.";
        return RedirectToAction(nameof(Index), new { estado = EstadoPublicacion.Publicada });
    }

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> Despublicar(int id)
    {
        var p = await _db.Publicaciones.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();

        p.Estado = EstadoPublicacion.Borrador;
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"\"{p.Titulo}\" volvió a borrador: ya no se ve en el boletín.";
        return RedirectToAction(nameof(Index), new { estado = EstadoPublicacion.Borrador });
    }

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> Eliminar(int id)
    {
        var p = await _db.Publicaciones.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();

        var ruta = p.ImagenRuta;
        var estado = p.Estado;
        _db.Publicaciones.Remove(p);
        await _db.SaveChangesAsync();
        if (ruta is not null) await _archivos.EliminarAsync(ruta);

        TempData["Mensaje"] = $"Eliminaste \"{p.Titulo}\".";
        return RedirectToAction(nameof(Index), new { estado });
    }

    // ---------- Apoyo ----------

    private async Task ValidarAsync(PublicacionFormViewModel model, bool nueva)
    {
        if (!Enum.IsDefined(model.Categoria))
            ModelState.AddModelError(nameof(model.Categoria), "Elige una categoría válida.");

        // Escenario 3: un evento necesita cuándo y dónde
        if (model.Categoria == CategoriaPublicacion.Evento)
        {
            if (model.FechaEvento is null)
                ModelState.AddModelError(nameof(model.FechaEvento), "Indica la fecha y la hora del evento.");
            else if (nueva && Formatos.Utc(model.FechaEvento.Value) <= DateTime.UtcNow)
                ModelState.AddModelError(nameof(model.FechaEvento), "La fecha del evento debe ser futura.");
            else if (model.FechaEvento > DateTime.Today.AddYears(2))
                ModelState.AddModelError(nameof(model.FechaEvento), "La fecha del evento no puede superar los 2 años.");
            if (string.IsNullOrWhiteSpace(model.LugarEvento))
                ModelState.AddModelError(nameof(model.LugarEvento), "Indica dónde es el evento (dirección o enlace si es virtual).");
        }

        // La causa debe ser de esta fundación (el filtro por ESAL lo garantiza)
        if (model.CausaId is int causaId && !await _db.Causas.AnyAsync(c => c.Id == causaId))
            ModelState.AddModelError(nameof(model.CausaId), "Elige una causa de la fundación.");
    }

    private async Task<ArchivoValidado?> ValidarImagenAsync(PublicacionFormViewModel model)
    {
        if (model.Imagen is null || model.Imagen.Length == 0) return null;
        var info = await ValidadorArchivos.ValidarAsync(model.Imagen, TipoArchivo.Imagen);
        if (!info.Valido)
        {
            ModelState.AddModelError(nameof(model.Imagen), info.Error!);
            return null;
        }
        return info;
    }

    private async Task<string> GuardarImagenAsync(IFormFile archivo, ArchivoValidado info)
    {
        await using var stream = archivo.OpenReadStream();
        return await _archivos.GuardarAsync(stream, $"esal/{EsalId}/boletin", info.Extension, publico: true);
    }

    private static void Copiar(PublicacionFormViewModel model, Publicacion p)
    {
        var esEvento = model.Categoria == CategoriaPublicacion.Evento;
        p.Titulo = model.Titulo.Trim();
        p.Categoria = model.Categoria;
        p.Resumen = string.IsNullOrWhiteSpace(model.Resumen) ? null : model.Resumen.Trim();
        p.Contenido = model.Contenido.Trim();
        p.FechaEvento = esEvento && model.FechaEvento is DateTime f ? Formatos.Utc(f) : null;
        p.LugarEvento = esEvento ? model.LugarEvento?.Trim() : null;
        p.CausaId = model.CausaId;
    }

    private async Task<PublicacionFormViewModel> PrepararAsync(PublicacionFormViewModel model, Publicacion? p)
    {
        model.Id = p?.Id;
        model.Estado = p?.Estado ?? EstadoPublicacion.Borrador;
        model.ImagenActualUrl = p?.ImagenRuta is null ? null : _archivos.UrlPublica(p.ImagenRuta);
        model.Causas = await _db.Causas.AsNoTracking()
            .OrderByDescending(c => c.FechaCreacion)
            .Select(c => new CausaOpcion(c.Id, c.Titulo)).ToListAsync();
        return model;
    }
}
