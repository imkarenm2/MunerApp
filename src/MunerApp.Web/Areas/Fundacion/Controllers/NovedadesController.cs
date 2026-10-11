using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Constantes;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Areas.Fundacion.Models;
using MunerApp.Web.Filtros;
using MunerApp.Web.Servicios;
using MunerApp.Web.Validacion;

namespace MunerApp.Web.Areas.Fundacion.Controllers;

/// <summary>
/// HU-022 escenario 1: el administrador publica novedades (texto y fotos) de un beneficiario para sus padrinos.
/// Los padrinos activos reciben una notificación y la ven en Mis apadrinamientos.
/// </summary>
[Area("Fundacion")]
[Authorize(Roles = Roles.AdministradorESAL)]
[RequiereModulo(CodigosModulo.Beneficiarios)]
public class NovedadesController : Controller
{
    private const long LimitePeticion = 17L * 1024 * 1024; // 3 fotos de hasta 5 MB y el texto

    private readonly MunerAppDbContext _db;
    private readonly IAlmacenamientoArchivos _archivos;
    private readonly EstadosBeneficiario _estados;

    public NovedadesController(MunerAppDbContext db, IAlmacenamientoArchivos archivos, EstadosBeneficiario estados)
    {
        _db = db;
        _archivos = archivos;
        _estados = estados;
    }

    private string UsuarioId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // ---------- Novedades publicadas de un beneficiario ----------

    [HttpGet]
    public async Task<IActionResult> Index(int id)
    {
        var b = await _db.Beneficiarios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();

        var novedades = await (from n in _db.NovedadesBeneficiario.AsNoTracking()
                               where n.BeneficiarioId == id
                               join u in _db.Users on n.PublicadaPorId equals u.Id into us
                               from u in us.DefaultIfEmpty()
                               orderby n.Fecha descending, n.Id descending
                               select new NovedadItem
                               {
                                   Id = n.Id,
                                   Texto = n.Texto,
                                   Fecha = n.Fecha,
                                   PublicadaPor = u == null ? null : u.NombreCompleto,
                                   FotoIds = n.Fotos.OrderBy(f => f.Id).Select(f => f.Id).ToList()
                               }).ToListAsync();

        return View(new NovedadesBeneficiarioViewModel
        {
            BeneficiarioId = b.Id,
            Nombre = b.Nombre,
            Estado = b.Estado,
            PadrinosActivos = await PadrinosActivosAsync(id),
            PuedePublicar = await PuedePublicarAsync(b),
            Novedades = novedades
        });
    }

    // ---------- Escenario 1: publicar ----------

    [HttpGet]
    public async Task<IActionResult> Crear(int id)
    {
        var b = await _db.Beneficiarios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();
        if (!await PuedePublicarAsync(b)) return SinPadrinos(b);

        return View(new NovedadFormViewModel { BeneficiarioId = id, NombreBeneficiario = b.Nombre, PadrinosActivos = await PadrinosActivosAsync(id) });
    }

    [HttpPost]
    [RequestSizeLimit(LimitePeticion)]
    public async Task<IActionResult> Crear(int id, NovedadFormViewModel model)
    {
        var b = await _db.Beneficiarios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();
        if (!await PuedePublicarAsync(b)) return SinPadrinos(b);
        model.BeneficiarioId = id;
        model.NombreBeneficiario = b.Nombre;
        model.PadrinosActivos = await PadrinosActivosAsync(id);

        var fotos = (model.Fotos ?? new List<IFormFile>()).Where(f => f.Length > 0).ToList();
        var validadas = new List<(IFormFile Archivo, ArchivoValidado Info)>();
        if (fotos.Count > NovedadBeneficiario.MaxFotos)
            ModelState.AddModelError(nameof(model.Fotos), $"Puedes adjuntar máximo {NovedadBeneficiario.MaxFotos} fotos.");
        else
        {
            foreach (var foto in fotos)
            {
                var info = await ValidadorArchivos.ValidarAsync(foto, TipoArchivo.Imagen);
                if (!info.Valido)
                    ModelState.AddModelError(nameof(model.Fotos), $"\"{Path.GetFileName(foto.FileName)}\": {info.Error}");
                else
                    validadas.Add((foto, info));
            }
        }
        if (!ModelState.IsValid) return View(model);

        var novedad = new NovedadBeneficiario
        {
            EsalId = b.EsalId,
            BeneficiarioId = id,
            Texto = model.Texto.Trim(),
            PublicadaPorId = UsuarioId
        };
        foreach (var (archivo, info) in validadas)
        {
            await using var stream = archivo.OpenReadStream();
            var clave = await _archivos.GuardarAsync(stream, $"esal/{b.EsalId}/novedades", info.Extension, publico: false);
            novedad.Fotos.Add(new FotoNovedad { EsalId = b.EsalId, Ruta = clave });
        }
        _db.NovedadesBeneficiario.Add(novedad);

        // Los padrinos activos reciben el aviso en la plataforma (se guarda junto con la novedad)
        var resumen = novedad.Texto.Length <= 140 ? novedad.Texto : novedad.Texto[..139] + "…";
        var avisados = await _estados.NotificarPadrinosAsync(b, $"Novedades de {b.Nombre}", resumen);
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = avisados switch
        {
            0 => $"Publicaste la novedad de {b.Nombre}. Todavía no tiene padrinos activos: la verán quienes lo apadrinen.",
            1 => $"Publicaste la novedad de {b.Nombre} y avisamos a su padrino.",
            _ => $"Publicaste la novedad de {b.Nombre} y avisamos a sus {avisados} padrinos."
        };
        return RedirectToAction(nameof(Index), new { id });
    }

    // ---------- Eliminar (por si se publicó por error) ----------

    [HttpPost]
    public async Task<IActionResult> Eliminar(int id)
    {
        var n = await _db.NovedadesBeneficiario.Include(x => x.Fotos).FirstOrDefaultAsync(x => x.Id == id);
        if (n is null) return NotFound();

        var rutas = n.Fotos.Select(f => f.Ruta).ToList();
        _db.NovedadesBeneficiario.Remove(n);
        await _db.SaveChangesAsync();
        foreach (var ruta in rutas) await _archivos.EliminarAsync(ruta); // después de guardar: si falla, la novedad ya no existe

        TempData["Mensaje"] = "Eliminaste la novedad. Los padrinos ya no la verán.";
        return RedirectToAction(nameof(Index), new { id = n.BeneficiarioId });
    }

    // ---------- Fotos privadas ----------

    [HttpGet]
    public async Task<IActionResult> Foto(int id)
    {
        var ruta = await _db.FotosNovedad.AsNoTracking().Where(f => f.Id == id).Select(f => f.Ruta).FirstOrDefaultAsync();
        if (ruta is null) return NotFound();
        var stream = await _archivos.AbrirAsync(ruta);
        if (stream is null) return NotFound();
        Response.Headers.CacheControl = "private,max-age=300";
        return File(stream, ValidadorArchivos.ContentTypeDe(ruta));
    }

    // ---------- Apoyo ----------

    private Task<int> PadrinosActivosAsync(int beneficiarioId) =>
        _db.Apadrinamientos.CountAsync(a => a.BeneficiarioId == beneficiarioId && a.Estado == EstadoApadrinamiento.Activo);

    /// <summary>Se publica para quien tiene padrinos activos o se ofrece para apadrinar (los futuros padrinos verán el historial).</summary>
    private async Task<bool> PuedePublicarAsync(Beneficiario b) => b.Apadrinable || await PadrinosActivosAsync(b.Id) > 0;

    private IActionResult SinPadrinos(Beneficiario b)
    {
        TempData["Error"] = $"{b.Nombre} no tiene padrinos activos ni se ofrece para apadrinar: no hay a quién contarle las novedades.";
        return RedirectToAction("Detalle", "Beneficiarios", new { id = b.Id });
    }
}
