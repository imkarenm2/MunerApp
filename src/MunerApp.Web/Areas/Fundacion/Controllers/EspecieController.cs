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
using MunerApp.Web.Seguridad;
using MunerApp.Web.Servicios;
using MunerApp.Web.Validacion;

namespace MunerApp.Web.Areas.Fundacion.Controllers;

/// <summary>
/// Donaciones en especie. Los administradores (principal y de consulta) ven lo que los donantes ofrecen y
/// registran lo que llega, como con las donaciones en dinero. Las necesidades que se publican en el perfil
/// las crea y cierra el administrador principal, porque son contenido público de la fundación.
/// </summary>
[Area("Fundacion")]
[Authorize(Roles = Roles.AdministradorESAL)]
public class EspecieController : Controller
{
    private readonly MunerAppDbContext _db;
    private readonly IAlmacenamientoArchivos _archivos;
    private readonly INotificacionService _notificaciones;
    private readonly AvisosCorreo _avisos;
    private readonly IEsalActual _esalActual;

    public EspecieController(MunerAppDbContext db, IAlmacenamientoArchivos archivos, INotificacionService notificaciones,
        AvisosCorreo avisos, IEsalActual esalActual)
    {
        _esalActual = esalActual;
        _db = db;
        _archivos = archivos;
        _notificaciones = notificaciones;
        _avisos = avisos;
    }

    private string UsuarioId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // ---------- Listado ----------

    [HttpGet]
    public async Task<IActionResult> Index(EstadoDonacionEspecie estado = EstadoDonacionEspecie.Ofrecida)
    {
        var conteos = await _db.DonacionesEspecie.AsNoTracking()
            .GroupBy(d => d.Estado)
            .Select(g => new { g.Key, N = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.N);

        var donaciones = await (from d in _db.DonacionesEspecie.AsNoTracking()
                                join u in _db.Users on d.DonanteId equals u.Id
                                where d.Estado == estado
                                orderby (estado == EstadoDonacionEspecie.Ofrecida ? d.FechaEntregaEstimada : d.FechaOferta) descending
                                select new DonacionEspecieItem
                                {
                                    Id = d.Id,
                                    Codigo = d.Codigo,
                                    Donante = u.NombreCompleto,
                                    Articulo = d.Articulo,
                                    Categoria = d.Categoria,
                                    Cantidad = d.CantidadRecibida ?? d.CantidadOfrecida,
                                    Unidad = d.Unidad,
                                    FormaEntrega = d.FormaEntrega,
                                    FechaEntregaEstimada = d.FechaEntregaEstimada,
                                    FechaOferta = d.FechaOferta,
                                    Estado = d.Estado
                                }).Take(200).ToListAsync();

        // Las pendientes se ordenan por la fecha en que deben llegar (la más próxima primero)
        if (estado == EstadoDonacionEspecie.Ofrecida)
            donaciones = donaciones.OrderBy(d => d.FechaEntregaEstimada).ToList();

        return View(new EspecieIndexViewModel
        {
            Estado = estado,
            Conteos = conteos,
            Donaciones = donaciones,
            Necesidades = await NecesidadesAsync(),
            PuedeGestionarNecesidades = Politicas.EsAdminPrincipal(User)
        });
    }

    // ---------- Detalle, recibir y marcar como no recibida ----------

    [HttpGet]
    public async Task<IActionResult> Detalle(int id)
    {
        var modelo = await CargarDetalleAsync(id);
        return modelo is null ? NotFound() : View(modelo);
    }

    [HttpPost]
    [RequestSizeLimit(ValidadorArchivos.LimitePeticionBytes)]
    public async Task<IActionResult> Recibir(int id, [Bind(Prefix = "Recibir")] RecibirEspecieViewModel model)
    {
        var d = await _db.DonacionesEspecie.Include(x => x.Esal).FirstOrDefaultAsync(x => x.Id == id);
        if (d is null) return NotFound();
        if (!d.Pendiente)
        {
            TempData["Error"] = $"La donación {d.Codigo} ya fue registrada como \"{Textos.De(d.Estado)}\".";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        if (model.FechaVencimiento is DateTime venc && venc.Date < Formatos.Hoy())
            ModelState.AddModelError("Recibir.FechaVencimiento", "Lo recibido ya está vencido: márcalo como no recibido o recíbelo y anótalo en las observaciones con una fecha válida.");

        ArchivoValidado? foto = null;
        if (model.Foto is { Length: > 0 })
        {
            foto = await ValidadorArchivos.ValidarAsync(model.Foto, TipoArchivo.Imagen);
            if (!foto.Valido) ModelState.AddModelError("Recibir.Foto", foto.Error!);
        }

        if (!ModelState.IsValid)
        {
            var detalle = await CargarDetalleAsync(id);
            detalle!.Recibir = model;
            return View(nameof(Detalle), detalle);
        }

        if (foto is not null)
        {
            await using var stream = model.Foto!.OpenReadStream();
            d.FotoRecepcionRuta = await _archivos.GuardarAsync(stream, $"esal/{d.EsalId}/especie", foto.Extension, publico: true);
        }

        d.Estado = EstadoDonacionEspecie.Recibida;
        d.CantidadRecibida = model.CantidadRecibida;
        d.FechaRecepcion = DateTime.UtcNow;
        d.FechaVencimiento = model.FechaVencimiento?.Date;
        d.ObservacionesRecepcion = string.IsNullOrWhiteSpace(model.Observaciones) ? null : model.Observaciones.Trim();
        d.RecibidaPorId = UsuarioId;

        var mensaje = $"{d.Esal!.Nombre} recibió tu donación en especie: {d.CantidadRecibida} {d.Unidad} de {d.Articulo}. Ya puedes descargar tu comprobante.";
        _notificaciones.Agregar(d.DonanteId, "¡Recibieron tu donación en especie!", mensaje, $"/mis-donaciones/especie/{d.Codigo}", "bi-box-seam");
        await _db.SaveChangesAsync();
        await _avisos.EnviarAUsuarioAsync(d.DonanteId, $"Recibieron tu donación {d.Codigo}", "¡Gracias por tu donación en especie!",
            mensaje, "Ver mi comprobante", $"/mis-donaciones/especie/{d.Codigo}");

        TempData["Mensaje"] = $"Registraste que llegó la donación {d.Codigo}. El donante ya tiene su comprobante.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> NoRecibida(int id, string? motivo)
    {
        var d = await _db.DonacionesEspecie.Include(x => x.Esal).FirstOrDefaultAsync(x => x.Id == id);
        if (d is null) return NotFound();
        if (!d.Pendiente)
        {
            TempData["Error"] = $"La donación {d.Codigo} ya fue registrada como \"{Textos.De(d.Estado)}\".";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        motivo = motivo?.Trim();
        if (string.IsNullOrEmpty(motivo) || motivo.Length < 5)
        {
            TempData["Error"] = "Escribe por qué no se recibió (por ejemplo: no llegó en la fecha acordada).";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        d.Estado = EstadoDonacionEspecie.NoRecibida;
        d.MotivoNoRecibida = motivo.Length > 300 ? motivo[..300] : motivo;
        d.RecibidaPorId = UsuarioId;
        d.FechaRecepcion = DateTime.UtcNow;
        _notificaciones.Agregar(d.DonanteId, "Tu donación en especie no se recibió",
            $"{d.Esal!.Nombre} registró que no recibió {d.Articulo}: {d.MotivoNoRecibida}", $"/mis-donaciones/especie/{d.Codigo}", "bi-box-seam");
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"Registraste que la donación {d.Codigo} no se recibió. Avisamos al donante.";
        return RedirectToAction(nameof(Index), new { estado = EstadoDonacionEspecie.NoRecibida });
    }

    // ---------- Necesidades (administrador principal) ----------

    [HttpGet]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public IActionResult CrearNecesidad() => View("Necesidad", new NecesidadFormViewModel());

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> CrearNecesidad(NecesidadFormViewModel model)
    {
        if (!ModelState.IsValid) return View("Necesidad", model);

        var n = new NecesidadEspecie
        {
            EsalId = _esalActual.EsalId ?? throw new InvalidOperationException("El usuario no pertenece a una ESAL."),
            CreadaPorId = UsuarioId
        };
        Copiar(model, n);
        _db.NecesidadesEspecie.Add(n);
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"Publicaste \"{n.Nombre}\". Ya aparece en el perfil de la fundación para que los donantes la vean.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> EditarNecesidad(int id)
    {
        var n = await _db.NecesidadesEspecie.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (n is null) return NotFound();
        return View("Necesidad", new NecesidadFormViewModel
        {
            Id = n.Id, Nombre = n.Nombre, Categoria = n.Categoria, Unidad = n.Unidad,
            CantidadMeta = n.CantidadMeta, Descripcion = n.Descripcion, Urgente = n.Urgente
        });
    }

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> EditarNecesidad(int id, NecesidadFormViewModel model)
    {
        var n = await _db.NecesidadesEspecie.FirstOrDefaultAsync(x => x.Id == id);
        if (n is null) return NotFound();
        if (!ModelState.IsValid) { model.Id = id; return View("Necesidad", model); }

        Copiar(model, n);
        await _db.SaveChangesAsync();
        TempData["Mensaje"] = $"Guardaste los cambios de \"{n.Nombre}\".";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Cierra o vuelve a abrir una necesidad (por ejemplo, cuando ya se cubrió).</summary>
    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> CambiarNecesidad(int id)
    {
        var n = await _db.NecesidadesEspecie.FirstOrDefaultAsync(x => x.Id == id);
        if (n is null) return NotFound();
        n.Activa = !n.Activa;
        await _db.SaveChangesAsync();
        TempData["Mensaje"] = n.Activa
            ? $"\"{n.Nombre}\" volvió a publicarse en el perfil."
            : $"Cerraste \"{n.Nombre}\": ya no se muestra en el perfil.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Apoyo ----------

    private static void Copiar(NecesidadFormViewModel m, NecesidadEspecie n)
    {
        n.Nombre = m.Nombre.Trim();
        n.Categoria = m.Categoria!.Value;
        n.Unidad = m.Unidad.Trim();
        n.CantidadMeta = m.CantidadMeta!.Value;
        n.Descripcion = string.IsNullOrWhiteSpace(m.Descripcion) ? null : m.Descripcion.Trim();
        n.Urgente = m.Urgente;
    }

    private async Task<IReadOnlyList<NecesidadItem>> NecesidadesAsync()
    {
        var necesidades = await _db.NecesidadesEspecie.AsNoTracking()
            .OrderByDescending(n => n.Activa).ThenByDescending(n => n.Urgente).ThenByDescending(n => n.FechaCreacion)
            .Select(n => new NecesidadItem
            {
                Id = n.Id, Nombre = n.Nombre, Categoria = n.Categoria, Unidad = n.Unidad, CantidadMeta = n.CantidadMeta,
                Urgente = n.Urgente, Activa = n.Activa,
                Recibido = _db.DonacionesEspecie.Where(d => d.NecesidadId == n.Id && d.Estado == EstadoDonacionEspecie.Recibida)
                    .Sum(d => (int?)d.CantidadRecibida) ?? 0,
                PorRecibir = _db.DonacionesEspecie.Where(d => d.NecesidadId == n.Id && d.Estado == EstadoDonacionEspecie.Ofrecida)
                    .Sum(d => (int?)d.CantidadOfrecida) ?? 0
            })
            .ToListAsync();
        return necesidades;
    }

    private async Task<DonacionEspecieDetalleViewModel?> CargarDetalleAsync(int id)
    {
        var d = await _db.DonacionesEspecie.AsNoTracking().Include(x => x.Necesidad).FirstOrDefaultAsync(x => x.Id == id);
        if (d is null) return null;
        var donante = await _db.Users.AsNoTracking().Where(u => u.Id == d.DonanteId).Select(u => new { u.NombreCompleto, u.Email }).FirstAsync();
        var recibio = d.RecibidaPorId is null ? null
            : await _db.Users.AsNoTracking().Where(u => u.Id == d.RecibidaPorId).Select(u => u.NombreCompleto).FirstOrDefaultAsync();

        return new DonacionEspecieDetalleViewModel
        {
            Id = d.Id,
            Codigo = d.Codigo,
            Donante = donante.NombreCompleto,
            CorreoDonante = donante.Email ?? "",
            Necesidad = d.Necesidad?.Nombre,
            Articulo = d.Articulo,
            Categoria = d.Categoria,
            CantidadOfrecida = d.CantidadOfrecida,
            Unidad = d.Unidad,
            FormaEntrega = d.FormaEntrega,
            FechaEntregaEstimada = d.FechaEntregaEstimada,
            DireccionRecogida = d.DireccionRecogida,
            Mensaje = d.Mensaje,
            Estado = d.Estado,
            FechaOferta = d.FechaOferta,
            CantidadRecibida = d.CantidadRecibida,
            FechaRecepcion = d.FechaRecepcion,
            FechaVencimiento = d.FechaVencimiento,
            ObservacionesRecepcion = d.ObservacionesRecepcion,
            FotoUrl = d.FotoRecepcionRuta is null ? null : _archivos.UrlPublica(d.FotoRecepcionRuta),
            MotivoNoRecibida = d.MotivoNoRecibida,
            RecibidaPor = recibio,
            Recibir = new RecibirEspecieViewModel { CantidadRecibida = d.CantidadOfrecida }
        };
    }
}
