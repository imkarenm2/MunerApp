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
using MunerApp.Web.Seguridad;
using MunerApp.Web.Servicios;
using MunerApp.Web.Validacion;

namespace MunerApp.Web.Areas.Fundacion.Controllers;

/// <summary>
/// HU-041: causas de recaudación (vakis). Los administradores de la ESAL ven el listado; publicar, editar y pausar
/// es del administrador principal, porque son contenidos públicos de la fundación (igual que el perfil y la transparencia).
/// El filtro global por ESAL garantiza que nunca se vea ni se modifique una causa de otra fundación.
/// </summary>
[Area("Fundacion")]
[Authorize(Roles = Roles.AdministradorESAL)]
public class CausasController : Controller
{
    public const int MaxFotos = 5;
    private const long LimitePeticion = 32L * 1024 * 1024; // 5 fotos de hasta 5 MB y los demás campos
    private const decimal MetaMaxima = 1_000_000_000;

    private readonly MunerAppDbContext _db;
    private readonly IEsalActual _esalActual;
    private readonly IAlmacenamientoArchivos _archivos;

    public CausasController(MunerAppDbContext db, IEsalActual esalActual, IAlmacenamientoArchivos archivos)
    {
        _db = db;
        _esalActual = esalActual;
        _archivos = archivos;
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
        var hoy = DateTime.Today;

        return View(new CausasIndexViewModel
        {
            Slug = await _db.Esales.AsNoTracking().Where(e => e.Id == EsalId).Select(e => e.Slug).FirstOrDefaultAsync(),
            PuedeGestionar = User.HasClaim(MunerAppClaims.Perfil, Perfiles.Principal),
            Causas = causas
                .Select(c => Item(c, recaudos.GetValueOrDefault(c.Id), hoy))
                .OrderBy(c => c.Cerrada).ThenByDescending(c => c.Id)
                .ToList()
        });
    }

    // ---------- Escenario 1 y 2: publicar ----------

    [HttpGet]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public IActionResult Crear() => View(new CausaFormViewModel { FechaLimite = DateTime.Today.AddDays(30) });

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    [RequestSizeLimit(LimitePeticion)]
    public async Task<IActionResult> Crear(CausaFormViewModel model)
    {
        var meta = ValidarCampos(model, recaudado: 0);

        // Al menos una foto
        var fotos = (model.Fotos ?? new List<IFormFile>()).Where(f => f.Length > 0).ToList();
        var validadas = await ValidarFotosAsync(model, fotos, fotosExistentes: 0, exigirUna: true);

        if (!ModelState.IsValid) return View(model);

        var causa = new Causa
        {
            EsalId = EsalId,
            Titulo = model.Titulo.Trim(),
            Descripcion = model.Descripcion.Trim(),
            Meta = meta!.Value,
            FechaLimite = model.FechaLimite!.Value.Date,
            Estado = EstadoCausa.Activa,
            CreadaPorId = UsuarioId
        };
        await AgregarFotosAsync(causa, validadas, ordenInicial: 0);
        _db.Causas.Add(causa);
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"Publicaste \"{causa.Titulo}\". Ya aparece en el perfil de la fundación con su barra de progreso en 0 %.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Escenario 3: editar, pausar y reanudar ----------

    [HttpGet]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> Editar(int id)
    {
        var c = await _db.Causas.AsNoTracking().Include(x => x.Fotos).FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();

        var recaudado = await RecaudadoAsync(id);
        if (c.EstaCerrada(recaudado, DateTime.Today))
        {
            TempData["Error"] = $"\"{c.Titulo}\" ya está cerrada y no se puede editar.";
            return RedirectToAction(nameof(Index));
        }

        return View(Formulario(c, recaudado, new CausaFormViewModel
        {
            Titulo = c.Titulo,
            Descripcion = c.Descripcion,
            Meta = ((long)c.Meta).ToString("N0", new System.Globalization.CultureInfo("es-CO")),
            FechaLimite = c.FechaLimite
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
        if (c.EstaCerrada(recaudado, DateTime.Today))
        {
            TempData["Error"] = $"\"{c.Titulo}\" ya está cerrada y no se puede editar.";
            return RedirectToAction(nameof(Index));
        }

        var meta = ValidarCampos(model, recaudado);
        var fotos = (model.Fotos ?? new List<IFormFile>()).Where(f => f.Length > 0).ToList();
        var validadas = await ValidarFotosAsync(model, fotos, c.Fotos.Count, exigirUna: false);
        if (!ModelState.IsValid) return View(Formulario(c, recaudado, model));

        c.Titulo = model.Titulo.Trim();
        c.Descripcion = model.Descripcion.Trim();
        c.Meta = meta!.Value;
        c.FechaLimite = model.FechaLimite!.Value.Date;
        await AgregarFotosAsync(c, validadas, ordenInicial: c.Fotos.Count == 0 ? 0 : c.Fotos.Max(f => f.Orden) + 1);
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"Guardaste los cambios de \"{c.Titulo}\". Ya se ven en el perfil.";
        return RedirectToAction(nameof(Index));
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

        if (c.EstaCerrada(await RecaudadoAsync(id), DateTime.Today))
        {
            TempData["Error"] = $"\"{c.Titulo}\" ya está cerrada.";
            return RedirectToAction(nameof(Index));
        }

        var nuevo = pausar ? EstadoCausa.Pausada : EstadoCausa.Activa;
        if (c.Estado == nuevo)
        {
            TempData["Error"] = $"\"{c.Titulo}\" ya estaba {(pausar ? "en pausa" : "activa")}.";
            return RedirectToAction(nameof(Index));
        }

        c.Estado = nuevo;
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = pausar
            ? $"Pausaste \"{c.Titulo}\". Se sigue viendo, pero no recibe donaciones hasta que la reanudes."
            : $"Reanudaste \"{c.Titulo}\". Vuelve a recibir donaciones.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Apoyo ----------

    /// <summary>Valida título, meta y fecha límite. Devuelve la meta ya convertida a pesos.</summary>
    private decimal? ValidarCampos(CausaFormViewModel model, decimal recaudado)
    {
        var meta = Formatos.LeerPesos(model.Meta);
        if (!string.IsNullOrWhiteSpace(model.Meta))
        {
            if (meta is null)
                ModelState.AddModelError(nameof(model.Meta), "Escribe la meta solo con números, por ejemplo 2.500.000.");
            else if (meta <= 0)
                ModelState.AddModelError(nameof(model.Meta), "La meta debe ser mayor a cero.");
            else if (meta > MetaMaxima)
                ModelState.AddModelError(nameof(model.Meta), $"La meta no puede superar {Formatos.Pesos(MetaMaxima)}.");
            else if (meta <= recaudado)
                ModelState.AddModelError(nameof(model.Meta), $"La meta debe ser mayor a lo ya recaudado ({Formatos.Pesos(recaudado)}).");
        }

        if (model.FechaLimite is DateTime fecha)
        {
            if (fecha.Date <= DateTime.Today)
                ModelState.AddModelError(nameof(model.FechaLimite), "La fecha límite debe ser posterior a hoy.");
            else if (fecha.Date > DateTime.Today.AddYears(2))
                ModelState.AddModelError(nameof(model.FechaLimite), "La fecha límite no puede superar los 2 años.");
        }
        return meta;
    }

    private async Task<List<(IFormFile Archivo, ArchivoValidado Info)>> ValidarFotosAsync(
        CausaFormViewModel model, List<IFormFile> fotos, int fotosExistentes, bool exigirUna)
    {
        var validadas = new List<(IFormFile, ArchivoValidado)>();
        if (exigirUna && fotos.Count == 0)
        {
            ModelState.AddModelError(nameof(model.Fotos), "Agrega al menos una foto de la causa.");
            return validadas;
        }
        if (fotosExistentes + fotos.Count > MaxFotos)
        {
            ModelState.AddModelError(nameof(model.Fotos), $"Una causa puede tener máximo {MaxFotos} fotos" +
                (fotosExistentes > 0 ? $" (ya tiene {fotosExistentes})." : "."));
            return validadas;
        }
        foreach (var foto in fotos)
        {
            var info = await ValidadorArchivos.ValidarAsync(foto, TipoArchivo.Imagen);
            if (!info.Valido)
                ModelState.AddModelError(nameof(model.Fotos), $"\"{Path.GetFileName(foto.FileName)}\": {info.Error}");
            else
                validadas.Add((foto, info));
        }
        return validadas;
    }

    private async Task AgregarFotosAsync(Causa causa, List<(IFormFile Archivo, ArchivoValidado Info)> fotos, int ordenInicial)
    {
        var orden = ordenInicial;
        foreach (var (archivo, info) in fotos)
        {
            await using var stream = archivo.OpenReadStream();
            var clave = await _archivos.GuardarAsync(stream, $"esal/{causa.EsalId}/causas", info.Extension, publico: true);
            causa.Fotos.Add(new FotoCausa { EsalId = causa.EsalId, Ruta = clave, Orden = orden++ });
        }
    }

    private CausaFormViewModel Formulario(Causa c, decimal recaudado, CausaFormViewModel model)
    {
        model.Id = c.Id;
        model.Recaudado = recaudado;
        model.FotosActuales = c.Fotos.OrderBy(f => f.Orden).ThenBy(f => f.Id).Select(f => new FotoCausaItem(f.Id, _archivos.UrlPublica(f.Ruta))).ToList();
        return model;
    }

    private CausaItem Item(Causa c, decimal recaudado, DateTime hoy) => new()
    {
        Id = c.Id,
        Titulo = c.Titulo,
        Meta = c.Meta,
        Recaudado = recaudado,
        FechaLimite = c.FechaLimite,
        Estado = c.Estado,
        Cerrada = c.EstaCerrada(recaudado, hoy),
        Porcentaje = Formatos.Porcentaje(recaudado, c.Meta),
        DiasRestantes = c.DiasRestantes(hoy),
        FotoUrl = c.Fotos.OrderBy(f => f.Orden).ThenBy(f => f.Id).Select(f => _archivos.UrlPublica(f.Ruta)).FirstOrDefault()
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
