using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Entities;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Areas.Fundacion.Models;
using MunerApp.Web.Seguridad;
using MunerApp.Web.Validacion;

namespace MunerApp.Web.Areas.Fundacion.Controllers;

/// <summary>HU-012: documentos de transparencia. No se borran: se ocultan y quedan en el historial.</summary>
[Area("Fundacion")]
[Authorize(Policy = Politicas.AdminEsalPrincipal)]
public class DocumentosController : Controller
{
    private readonly MunerAppDbContext _db;
    private readonly IEsalActual _esalActual;
    private readonly IAlmacenamientoArchivos _archivos;

    public DocumentosController(MunerAppDbContext db, IEsalActual esalActual, IAlmacenamientoArchivos archivos)
    {
        _db = db;
        _esalActual = esalActual;
        _archivos = archivos;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var documentos = await _db.DocumentosTransparencia.AsNoTracking()
            .OrderByDescending(d => d.Visible).ThenByDescending(d => d.FechaPublicacion)
            .ToListAsync();

        return View(documentos.Select(d => new DocumentoItem
        {
            Id = d.Id,
            Titulo = d.Titulo,
            Categoria = d.Categoria,
            Descripcion = d.Descripcion,
            Url = _archivos.UrlPublica(d.Ruta),
            Extension = d.Extension,
            TamanoBytes = d.TamanoBytes,
            Visible = d.Visible,
            FechaPublicacion = d.FechaPublicacion,
            FechaOcultado = d.FechaOcultado
        }).ToList());
    }

    [HttpGet]
    public IActionResult Crear() => View(new DocumentoFormViewModel());

    [HttpPost]
    [RequestSizeLimit(ValidadorArchivos.LimitePeticionBytes)]
    public async Task<IActionResult> Crear(DocumentoFormViewModel model)
    {
        // Escenario 2: archivo no permitido
        var archivo = await ValidadorArchivos.ValidarAsync(model.Archivo, TipoArchivo.Documento);
        if (!archivo.Valido) ModelState.AddModelError(nameof(model.Archivo), archivo.Error!);
        if (!ModelState.IsValid) return View(model);

        var esalId = _esalActual.EsalId ?? throw new InvalidOperationException("El usuario no pertenece a una ESAL.");
        string clave;
        await using (var stream = model.Archivo!.OpenReadStream())
            clave = await _archivos.GuardarAsync(stream, $"esal/{esalId}/documentos", archivo.Extension, publico: true);

        _db.DocumentosTransparencia.Add(new DocumentoTransparencia
        {
            EsalId = esalId,
            Titulo = model.Titulo.Trim(),
            Categoria = model.Categoria!.Value,
            Descripcion = string.IsNullOrWhiteSpace(model.Descripcion) ? null : model.Descripcion.Trim(),
            Ruta = clave,
            NombreOriginal = Path.GetFileName(model.Archivo.FileName),
            Extension = archivo.Extension,
            TamanoBytes = model.Archivo.Length,
            PublicadoPorId = User.FindFirstValue(ClaimTypes.NameIdentifier)
        });
        await _db.SaveChangesAsync();

        // Escenario 1: aparece en la sección "Transparencia" del perfil con su fecha
        TempData["Mensaje"] = $"Publicaste \"{model.Titulo.Trim()}\". Ya aparece en la sección Transparencia del perfil.";
        return RedirectToAction(nameof(Index));
    }

    // Escenario 3: ocultar sin borrar
    [HttpPost]
    public async Task<IActionResult> Ocultar(int id)
    {
        var doc = await _db.DocumentosTransparencia.FirstOrDefaultAsync(d => d.Id == id);
        if (doc is null) return NotFound();

        doc.Visible = false;
        doc.FechaOcultado = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"Ocultaste \"{doc.Titulo}\". Ya no se ve en el perfil, pero queda en el historial.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Mostrar(int id)
    {
        var doc = await _db.DocumentosTransparencia.FirstOrDefaultAsync(d => d.Id == id);
        if (doc is null) return NotFound();

        doc.Visible = true;
        doc.FechaOcultado = null;
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"\"{doc.Titulo}\" vuelve a verse en el perfil.";
        return RedirectToAction(nameof(Index));
    }
}
