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
/// HU-017: hoja de vida de los beneficiarios (en el piloto, los gatos). Información interna de la fundación.
/// Solo la gestionan los administradores de la ESAL y solo si tiene activo el módulo de beneficiarios.
/// El filtro global por ESAL garantiza que nunca se vea un beneficiario de otra fundación.
/// </summary>
[Area("Fundacion")]
[Authorize(Roles = Roles.AdministradorESAL)]
[RequiereModulo(CodigosModulo.Beneficiarios)]
public class BeneficiariosController : Controller
{
    private readonly MunerAppDbContext _db;
    private readonly IEsalActual _esalActual;
    private readonly IAlmacenamientoArchivos _archivos;

    public BeneficiariosController(MunerAppDbContext db, IEsalActual esalActual, IAlmacenamientoArchivos archivos)
    {
        _db = db;
        _esalActual = esalActual;
        _archivos = archivos;
    }

    private string UsuarioId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private int EsalId => _esalActual.EsalId ?? throw new InvalidOperationException("El usuario no pertenece a una ESAL.");

    // ---------- Listado (la versión con filtros es la HU-018) ----------

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var items = await _db.Beneficiarios.AsNoTracking()
            .OrderBy(b => b.Nombre)
            .Select(b => new BeneficiarioItem
            {
                Id = b.Id,
                Nombre = b.Nombre,
                FechaNacimiento = b.FechaNacimiento,
                Sexo = b.Sexo,
                Color = b.Color,
                Estado = b.Estado,
                TieneFoto = b.FotoRuta != null,
                FaltaAdoptante = b.Estado == EstadoBeneficiario.Adoptado && b.Adoptante == null
            }).ToListAsync();

        return View(new BeneficiariosIndexViewModel { Beneficiarios = items });
    }

    // ---------- Escenario 1: registro exitoso ----------

    [HttpGet]
    public IActionResult Crear() => View(new BeneficiarioFormViewModel { FechaRescate = DateTime.Today });

    [HttpPost]
    [RequestSizeLimit(ValidadorArchivos.LimitePeticionBytes)]
    public async Task<IActionResult> Crear(BeneficiarioFormViewModel model)
    {
        var foto = await ValidarAsync(model);
        if (!ModelState.IsValid) return View(model);

        string? clave = null;
        if (foto is not null)
        {
            await using var stream = model.Foto!.OpenReadStream();
            clave = await _archivos.GuardarAsync(stream, $"esal/{EsalId}/beneficiarios", foto.Extension, publico: false);
        }

        var b = new Beneficiario
        {
            EsalId = EsalId,
            Nombre = model.Nombre.Trim(),
            FechaNacimiento = Nacimiento(model),
            Sexo = model.Sexo!.Value,
            Color = model.Color.Trim(),
            FechaRescate = model.FechaRescate!.Value.Date,
            Estado = EstadoBeneficiario.EnLaFundacion, // estado inicial fijo
            FotoRuta = clave,
            RegistradoPorId = UsuarioId
        };
        b.HistorialEstados.Add(new HistorialEstadoBeneficiario
        {
            EsalId = b.EsalId,
            Estado = EstadoBeneficiario.EnLaFundacion,
            CambiadoPorId = UsuarioId,
            Nota = "Registro de la hoja de vida"
        });
        _db.Beneficiarios.Add(b);
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"Registraste a {b.Nombre}. Su estado inicial es \"En la fundación\".";
        return RedirectToAction(nameof(Detalle), new { id = b.Id });
    }

    // ---------- Hoja de vida ----------

    [HttpGet]
    public async Task<IActionResult> Detalle(int id)
    {
        var b = await _db.Beneficiarios.AsNoTracking()
            .Include(x => x.Adoptante)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();

        var historial = await (from h in _db.HistorialEstadosBeneficiario.AsNoTracking()
                               where h.BeneficiarioId == id
                               join u in _db.Users on h.CambiadoPorId equals u.Id into us
                               from u in us.DefaultIfEmpty()
                               orderby h.Fecha descending, h.Id descending
                               select new CambioEstadoItem(h.Estado, h.Fecha, h.Nota, u == null ? null : u.NombreCompleto))
            .ToListAsync();

        var registrador = b.RegistradoPorId is null ? null
            : await _db.Users.AsNoTracking().Where(u => u.Id == b.RegistradoPorId).Select(u => u.NombreCompleto).FirstOrDefaultAsync();

        return View(new BeneficiarioDetalleViewModel
        {
            Id = b.Id,
            Nombre = b.Nombre,
            FechaNacimiento = b.FechaNacimiento,
            Sexo = b.Sexo,
            Color = b.Color,
            Estado = b.Estado,
            TieneFoto = b.FotoRuta is not null,
            FechaRescate = b.FechaRescate,
            FechaRegistro = b.FechaRegistro,
            RegistradoPor = registrador,
            FaltaAdoptante = b.Estado == EstadoBeneficiario.Adoptado && b.Adoptante is null,
            Historial = historial,
            Adoptante = b.Adoptante is null ? null : new AdoptanteItem
            {
                Nombre = b.Adoptante.Nombre,
                Documento = b.Adoptante.Documento,
                Telefono = b.Adoptante.Telefono,
                Correo = b.Adoptante.Correo,
                Ciudad = b.Adoptante.Ciudad,
                Direccion = b.Adoptante.Direccion,
                FechaAdopcion = b.Adoptante.FechaAdopcion,
                Observaciones = b.Adoptante.Observaciones
            }
        });
    }

    /// <summary>La foto es interna: se entrega solo a usuarios de la fundación dueña del beneficiario.</summary>
    [HttpGet]
    public async Task<IActionResult> Foto(int id)
    {
        var ruta = await _db.Beneficiarios.AsNoTracking().Where(b => b.Id == id).Select(b => b.FotoRuta).FirstOrDefaultAsync();
        if (ruta is null) return NotFound();
        var stream = await _archivos.AbrirAsync(ruta);
        if (stream is null) return NotFound();
        Response.Headers.CacheControl = "private,max-age=300";
        return File(stream, ValidadorArchivos.ContentTypeDe(ruta));
    }

    // ---------- Edición de los datos ----------

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var b = await _db.Beneficiarios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();

        var (anios, meses) = Formatos.AniosYMeses(b.FechaNacimiento);
        return View(new BeneficiarioFormViewModel
        {
            Id = b.Id,
            Nombre = b.Nombre,
            EdadAnios = anios,
            EdadMeses = meses,
            Sexo = b.Sexo,
            Color = b.Color,
            FechaRescate = b.FechaRescate,
            TieneFoto = b.FotoRuta is not null
        });
    }

    [HttpPost]
    [RequestSizeLimit(ValidadorArchivos.LimitePeticionBytes)]
    public async Task<IActionResult> Editar(int id, BeneficiarioFormViewModel model)
    {
        var b = await _db.Beneficiarios.FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();

        model.Id = id;
        model.TieneFoto = b.FotoRuta is not null;
        var foto = await ValidarAsync(model);
        if (!ModelState.IsValid) return View(model);

        // La fecha de nacimiento solo se recalcula si cambiaron los años o los meses
        var (aniosActuales, mesesActuales) = Formatos.AniosYMeses(b.FechaNacimiento);
        if (model.EdadAnios != aniosActuales || model.EdadMeses != mesesActuales)
            b.FechaNacimiento = Nacimiento(model);

        b.Nombre = model.Nombre.Trim();
        b.Sexo = model.Sexo!.Value;
        b.Color = model.Color.Trim();
        b.FechaRescate = model.FechaRescate!.Value.Date;

        string? fotoAnterior = null;
        if (foto is not null)
        {
            fotoAnterior = b.FotoRuta;
            await using var stream = model.Foto!.OpenReadStream();
            b.FotoRuta = await _archivos.GuardarAsync(stream, $"esal/{b.EsalId}/beneficiarios", foto.Extension, publico: false);
        }
        else if (model.QuitarFoto && b.FotoRuta is not null)
        {
            fotoAnterior = b.FotoRuta;
            b.FotoRuta = null;
        }

        await _db.SaveChangesAsync();
        if (fotoAnterior is not null) await _archivos.EliminarAsync(fotoAnterior);

        TempData["Mensaje"] = $"Guardaste los cambios de {b.Nombre}.";
        return RedirectToAction(nameof(Detalle), new { id });
    }

    // ---------- Escenario 2: cambio de estado ----------

    [HttpPost]
    public async Task<IActionResult> CambiarEstado(int id, CambioEstadoViewModel model)
    {
        var b = await _db.Beneficiarios.FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();

        if (!ModelState.IsValid || model.Estado is null || !Enum.IsDefined(model.Estado.Value))
        {
            TempData["Error"] = "Selecciona el nuevo estado.";
            return RedirectToAction(nameof(Detalle), new { id });
        }
        if (model.Estado == b.Estado)
        {
            TempData["Error"] = $"{b.Nombre} ya está en estado \"{Textos.De(b.Estado)}\".";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        var nuevo = model.Estado.Value;
        b.Estado = nuevo;
        _db.HistorialEstadosBeneficiario.Add(new HistorialEstadoBeneficiario
        {
            EsalId = b.EsalId,
            BeneficiarioId = b.Id,
            Estado = nuevo,
            CambiadoPorId = UsuarioId,
            Nota = string.IsNullOrWhiteSpace(model.Nota) ? null : model.Nota.Trim()
        });
        await _db.SaveChangesAsync();

        if (nuevo == EstadoBeneficiario.Adoptado && !await _db.AdoptantesBeneficiario.AnyAsync(a => a.BeneficiarioId == id))
        {
            TempData["Mensaje"] = $"{b.Nombre} ahora está \"Adoptado\". Registra los datos del adoptante para el seguimiento.";
            return RedirectToAction(nameof(Adoptante), new { id });
        }

        TempData["Mensaje"] = $"El estado de {b.Nombre} cambió a \"{Textos.De(nuevo)}\".";
        return RedirectToAction(nameof(Detalle), new { id });
    }

    // ---------- Escenario 3: datos del adoptante ----------

    [HttpGet]
    public async Task<IActionResult> Adoptante(int id)
    {
        var b = await _db.Beneficiarios.AsNoTracking().Include(x => x.Adoptante).FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();
        if (b.Estado != EstadoBeneficiario.Adoptado)
        {
            TempData["Error"] = "Los datos del adoptante solo se registran cuando el beneficiario está en estado \"Adoptado\".";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        var a = b.Adoptante;
        return View(new AdoptanteFormViewModel
        {
            BeneficiarioId = b.Id,
            NombreBeneficiario = b.Nombre,
            FechaRescate = b.FechaRescate,
            YaRegistrado = a is not null,
            Nombre = a?.Nombre ?? "",
            Documento = a?.Documento ?? "",
            Telefono = a?.Telefono ?? "",
            Correo = a?.Correo,
            Ciudad = a?.Ciudad ?? "",
            Direccion = a?.Direccion ?? "",
            FechaAdopcion = a?.FechaAdopcion ?? DateTime.Today,
            Observaciones = a?.Observaciones
        });
    }

    [HttpPost]
    public async Task<IActionResult> Adoptante(int id, AdoptanteFormViewModel model)
    {
        var b = await _db.Beneficiarios.Include(x => x.Adoptante).FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();
        if (b.Estado != EstadoBeneficiario.Adoptado)
        {
            TempData["Error"] = "Los datos del adoptante solo se registran cuando el beneficiario está en estado \"Adoptado\".";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        if (model.FechaAdopcion is DateTime fecha)
        {
            if (fecha.Date > DateTime.Today)
                ModelState.AddModelError(nameof(model.FechaAdopcion), "La fecha de adopción no puede ser futura.");
            else if (fecha.Date < b.FechaRescate.Date)
                ModelState.AddModelError(nameof(model.FechaAdopcion), $"La fecha de adopción no puede ser anterior al rescate ({Formatos.Fecha(b.FechaRescate)}).");
        }

        model.BeneficiarioId = b.Id;
        model.NombreBeneficiario = b.Nombre;
        model.FechaRescate = b.FechaRescate;
        model.YaRegistrado = b.Adoptante is not null;
        if (!ModelState.IsValid) return View(model);

        var a = b.Adoptante;
        if (a is null)
        {
            a = new AdoptanteBeneficiario { BeneficiarioId = b.Id, EsalId = b.EsalId };
            _db.AdoptantesBeneficiario.Add(a);
        }
        a.Nombre = model.Nombre.Trim();
        a.Documento = model.Documento.Trim();
        a.Telefono = model.Telefono.Trim();
        a.Correo = string.IsNullOrWhiteSpace(model.Correo) ? null : model.Correo.Trim();
        a.Ciudad = model.Ciudad.Trim();
        a.Direccion = model.Direccion.Trim();
        a.FechaAdopcion = model.FechaAdopcion!.Value.Date;
        a.Observaciones = string.IsNullOrWhiteSpace(model.Observaciones) ? null : model.Observaciones.Trim();
        a.RegistradoPorId = UsuarioId;
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"Guardaste los datos del adoptante de {b.Nombre}.";
        return RedirectToAction(nameof(Detalle), new { id });
    }

    // ---------- Apoyo ----------

    private async Task<ArchivoValidado?> ValidarAsync(BeneficiarioFormViewModel model)
    {
        if (model.FechaRescate is DateTime rescate && rescate.Date > DateTime.Today)
            ModelState.AddModelError(nameof(model.FechaRescate), "La fecha de rescate no puede ser futura.");

        if (model.Foto is null || model.Foto.Length == 0) return null;
        var foto = await ValidadorArchivos.ValidarAsync(model.Foto, TipoArchivo.Imagen);
        if (!foto.Valido) ModelState.AddModelError(nameof(model.Foto), foto.Error!);
        return foto.Valido ? foto : null;
    }

    private static DateTime Nacimiento(BeneficiarioFormViewModel m)
        => DateTime.Today.AddYears(-m.EdadAnios).AddMonths(-m.EdadMeses);
}
