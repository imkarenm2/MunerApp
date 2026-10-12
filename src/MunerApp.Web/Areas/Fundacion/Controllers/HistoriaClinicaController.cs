using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Application.Seguridad;
using MunerApp.Domain.Constantes;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Web.Servicios;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Areas.Fundacion.Models;
using MunerApp.Web.Filtros;
using MunerApp.Web.Seguridad;
using MunerApp.Web.Validacion;

namespace MunerApp.Web.Areas.Fundacion.Controllers;

/// <summary>
/// HU-019: historia clínica de cada beneficiario (vacunas, tratamientos, controles y fotos).
/// La ven los administradores (principal y de consulta) y los voluntarios de salud (practicantes);
/// la registran el administrador principal y los voluntarios de salud. El voluntario general no tiene acceso. Las fotos son privadas y se entregan por esta clase.
/// Los eventos no se editan ni se borran: la historia clínica se conserva completa.
/// </summary>
[Area("Fundacion")]
[Authorize(Policy = Politicas.AccesoClinico)]
[RequiereModulo(CodigosModulo.Beneficiarios)]
public class HistoriaClinicaController : Controller
{
    private const int MaxFotos = 5;
    private const long LimitePeticion = 27L * 1024 * 1024; // 5 fotos de hasta 5 MB y los demás campos

    private readonly MunerAppDbContext _db;
    private readonly IAlmacenamientoArchivos _archivos;

    public HistoriaClinicaController(MunerAppDbContext db, IAlmacenamientoArchivos archivos)
    {
        _db = db;
        _archivos = archivos;
    }

    private string UsuarioId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // ---------- Historia clínica en orden cronológico ----------

    [HttpGet]
    public async Task<IActionResult> Index(int id)
    {
        var b = await _db.Beneficiarios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();

        var eventos = await (from e in _db.EventosClinicos.AsNoTracking()
                             where e.BeneficiarioId == id
                             join u in _db.Users on e.RegistradoPorId equals u.Id into us
                             from u in us.DefaultIfEmpty()
                             orderby e.Fecha, e.Id
                             select new EventoClinicoItem
                             {
                                 Id = e.Id,
                                 Tipo = e.Tipo,
                                 Fecha = e.Fecha,
                                 Descripcion = e.Descripcion,
                                 Responsable = e.Responsable,
                                 Producto = e.Producto,
                                 Laboratorio = e.Laboratorio,
                                 PesoKg = e.PesoKg,
                                 Resultado = e.Resultado,
                                 RegistradoPor = u == null ? null : u.NombreCompleto,
                                 FechaRegistro = e.FechaRegistro,
                                 FotoIds = e.Fotos.OrderBy(f => f.Id).Select(f => f.Id).ToList()
                             }).ToListAsync();

        return View(new HistoriaClinicaViewModel
        {
            BeneficiarioId = b.Id,
            Nombre = b.Nombre,
            Estado = b.Estado,
            FechaNacimiento = b.FechaNacimiento,
            TieneFoto = b.FotoRuta is not null,
            Eventos = eventos,
            PuedeRegistrar = Politicas.PuedeRegistrarClinica(User)
        });
    }

    // ---------- Escenario 1: registrar un evento ----------

    [HttpGet]
    [Authorize(Policy = Politicas.RegistroClinico)]
    public async Task<IActionResult> Crear(int id)
    {
        var nombre = await _db.Beneficiarios.AsNoTracking().Where(b => b.Id == id).Select(b => b.Nombre).FirstOrDefaultAsync();
        if (nombre is null) return NotFound();

        return View(new EventoClinicoFormViewModel
        {
            BeneficiarioId = id,
            NombreBeneficiario = nombre,
            Fecha = DateTime.Today,
            Responsable = User.FindFirstValue(MunerAppClaims.NombreCompleto) ?? ""
        });
    }

    [HttpPost]
    [Authorize(Policy = Politicas.RegistroClinico)]
    [RequestSizeLimit(LimitePeticion)]
    public async Task<IActionResult> Crear(int id, EventoClinicoFormViewModel model)
    {
        var b = await _db.Beneficiarios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();
        model.BeneficiarioId = id;
        model.NombreBeneficiario = b.Nombre;

        if (model.Fecha is DateTime fecha && fecha.Date > DateTime.Today)
            ModelState.AddModelError(nameof(model.Fecha), "La fecha no puede ser futura.");

        // Campos según el tipo (formato de la fundación: vacunación, desparasitación y pruebas virales)
        var usaProducto = model.Tipo is TipoEventoClinico.Vacuna or TipoEventoClinico.Desparasitacion or TipoEventoClinico.PruebaViral
                                     or TipoEventoClinico.Tratamiento or TipoEventoClinico.Cirugia;
        if (model.Tipo is TipoEventoClinico.Vacuna or TipoEventoClinico.Desparasitacion or TipoEventoClinico.PruebaViral
            && string.IsNullOrWhiteSpace(model.Producto))
            ModelState.AddModelError(nameof(model.Producto), model.Tipo switch
            {
                TipoEventoClinico.Vacuna => "Indica qué vacuna se aplicó.",
                TipoEventoClinico.Desparasitacion => "Indica el desparasitante.",
                _ => "Indica la prueba (VIF o FeLV)."
            });
        if (model.Tipo == TipoEventoClinico.PruebaViral && string.IsNullOrWhiteSpace(model.Resultado))
            ModelState.AddModelError(nameof(model.Resultado), "Selecciona el resultado de la prueba.");
        decimal? peso = null;
        if (!string.IsNullOrWhiteSpace(model.Peso))
        {
            peso = Formatos.LeerDecimal(model.Peso);
            if (peso is null || peso <= 0 || peso > 100)
                ModelState.AddModelError(nameof(model.Peso), "Escribe el peso en kilos, por ejemplo 3,5.");
        }

        // Escenario 2: las fotos deben tener un formato permitido
        var fotos = (model.Fotos ?? new List<IFormFile>()).Where(f => f.Length > 0).ToList();
        var validadas = new List<(IFormFile Archivo, ArchivoValidado Info)>();
        if (fotos.Count > MaxFotos)
            ModelState.AddModelError(nameof(model.Fotos), $"Puedes adjuntar máximo {MaxFotos} fotos por evento.");
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

        var evento = new EventoClinico
        {
            EsalId = b.EsalId,
            BeneficiarioId = id,
            Tipo = model.Tipo!.Value,
            Fecha = model.Fecha!.Value.Date,
            Descripcion = model.Descripcion.Trim(),
            Responsable = model.Responsable.Trim(),
            Producto = usaProducto && !string.IsNullOrWhiteSpace(model.Producto) ? model.Producto.Trim() : null,
            Laboratorio = model.Tipo == TipoEventoClinico.Vacuna && !string.IsNullOrWhiteSpace(model.Laboratorio) ? model.Laboratorio.Trim() : null,
            PesoKg = peso,
            Resultado = model.Tipo == TipoEventoClinico.PruebaViral ? model.Resultado?.Trim() : null,
            RegistradoPorId = UsuarioId
        };
        foreach (var (archivo, info) in validadas)
        {
            await using var stream = archivo.OpenReadStream();
            var clave = await _archivos.GuardarAsync(stream, $"esal/{b.EsalId}/clinica", info.Extension, publico: false);
            evento.Fotos.Add(new FotoEventoClinico { EsalId = b.EsalId, Ruta = clave });
        }
        _db.EventosClinicos.Add(evento);
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"Agregaste el evento a la historia clínica de {b.Nombre}.";
        return RedirectToAction(nameof(Index), new { id });
    }

    // ---------- Fotos privadas ----------

    [HttpGet]
    public async Task<IActionResult> Foto(int id)
    {
        var ruta = await _db.FotosEventoClinico.AsNoTracking().Where(f => f.Id == id).Select(f => f.Ruta).FirstOrDefaultAsync();
        if (ruta is null) return NotFound();
        var stream = await _archivos.AbrirAsync(ruta);
        if (stream is null) return NotFound();
        Response.Headers.CacheControl = "private,max-age=300";
        return File(stream, ValidadorArchivos.ContentTypeDe(ruta));
    }
}
