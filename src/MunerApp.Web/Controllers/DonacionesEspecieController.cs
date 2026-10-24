using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Constantes;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Models.Publico;
using MunerApp.Web.Seguridad;
using MunerApp.Web.Servicios;

namespace MunerApp.Web.Controllers;

/// <summary>
/// Donaciones en especie del lado del donante: ver lo que necesita la fundación, ofrecer artículos,
/// seguir su estado y descargar el comprobante cuando la fundación confirma que los recibió.
/// Las acciones no se llaman Index para no confundir los enlaces con el EspecieController del área Fundacion.
/// </summary>
public class DonacionesEspecieController : Controller
{
    private const int DiasMaximosEntrega = 60;

    private readonly MunerAppDbContext _db;
    private readonly IAlmacenamientoArchivos _archivos;
    private readonly INotificacionService _notificaciones;
    private readonly IComprobanteService _comprobantes;

    public DonacionesEspecieController(MunerAppDbContext db, IAlmacenamientoArchivos archivos,
        INotificacionService notificaciones, IComprobanteService comprobantes)
    {
        _db = db;
        _archivos = archivos;
        _notificaciones = notificaciones;
        _comprobantes = comprobantes;
    }

    private string UsuarioId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // ---------- Página pública: lo que necesita la fundación ----------

    [HttpGet("fundaciones/{slug}/donar-en-especie")]
    public async Task<IActionResult> Necesidades(string slug)
    {
        var esal = await BuscarAsync(slug);
        if (esal is null) return NotFound();

        var necesidades = await _db.NecesidadesEspecie.IgnoreQueryFilters().AsNoTracking()
            .Where(n => n.EsalId == esal.Id && n.Activa)
            .OrderByDescending(n => n.Urgente).ThenByDescending(n => n.FechaCreacion)
            .Select(n => new
            {
                n.Id, n.Nombre, n.Categoria, n.Unidad, n.CantidadMeta, n.Descripcion, n.Urgente,
                Recibido = _db.DonacionesEspecie.IgnoreQueryFilters()
                    .Where(d => d.NecesidadId == n.Id && d.Estado == EstadoDonacionEspecie.Recibida)
                    .Sum(d => (int?)d.CantidadRecibida) ?? 0
            })
            .ToListAsync();

        return View(new DonarEspecieViewModel
        {
            Slug = esal.Slug!,
            NombreEsal = esal.Nombre,
            LogoUrl = UrlArchivo(esal.LogoRuta),
            Direccion = string.Join(" · ", new[] { esal.Ciudad, esal.Telefono }.Where(x => !string.IsNullOrWhiteSpace(x))),
            Necesidades = necesidades.Select(n => new NecesidadPublica(n.Id, n.Nombre, n.Categoria, n.Unidad, n.CantidadMeta, n.Recibido, n.Descripcion, n.Urgente)).ToList(),
            Recibido = await RecibidoUltimoAnioAsync(esal.Id)
        });
    }

    // ---------- Ofrecer una donación en especie (solo donantes) ----------

    [Authorize]
    [HttpGet("fundaciones/{slug}/donar-en-especie/ofrecer")]
    public async Task<IActionResult> Ofrecer(string slug, int? necesidad)
    {
        if (!Politicas.PuedeApoyar(User)) return CuentaInstitucional(slug);
        var esal = await BuscarAsync(slug);
        if (esal is null) return NotFound();

        var modelo = new OfrecerEspecieViewModel { FechaEntrega = Formatos.Hoy().AddDays(3) };
        if (necesidad is int id)
        {
            var n = await BuscarNecesidadAsync(esal.Id, id);
            if (n is null)
            {
                TempData["Error"] = "Esa necesidad ya no está publicada. Puedes ofrecer otra cosa.";
                return RedirectToAction(nameof(Necesidades), new { slug });
            }
            modelo.NecesidadId = n.Id;
            modelo.NombreNecesidad = n.Nombre;
            modelo.Articulo = n.Nombre;
            modelo.Categoria = n.Categoria;
            modelo.Unidad = n.Unidad;
        }
        return View(Preparar(modelo, esal));
    }

    [Authorize]
    [HttpPost("fundaciones/{slug}/donar-en-especie/ofrecer")]
    [EnableRateLimiting(LimitesUso.Formularios)]
    public async Task<IActionResult> Ofrecer(string slug, OfrecerEspecieViewModel model)
    {
        if (!Politicas.PuedeApoyar(User)) return CuentaInstitucional(slug);
        var esal = await BuscarAsync(slug);
        if (esal is null) return NotFound();

        NecesidadEspecie? necesidad = null;
        if (model.NecesidadId is int id)
        {
            necesidad = await BuscarNecesidadAsync(esal.Id, id);
            if (necesidad is null) model.NecesidadId = null;
        }

        var hoy = Formatos.Hoy();
        if (model.FechaEntrega is DateTime f && (f.Date < hoy || f.Date > hoy.AddDays(DiasMaximosEntrega)))
            ModelState.AddModelError(nameof(model.FechaEntrega), $"Elige una fecha entre hoy y los próximos {DiasMaximosEntrega} días.");
        if (model.FormaEntrega == FormaEntregaEspecie.Domicilio && string.IsNullOrWhiteSpace(model.DireccionRecogida))
            ModelState.AddModelError(nameof(model.DireccionRecogida), "Escribe la dirección donde la fundación puede recogerla.");

        if (!ModelState.IsValid) return View(Preparar(model, esal, necesidad));

        var donacion = new DonacionEspecie
        {
            EsalId = esal.Id,
            Codigo = "TMP-" + Guid.NewGuid().ToString("N")[..16],
            DonanteId = UsuarioId,
            NecesidadId = necesidad?.Id,
            // Si responde a una necesidad, el artículo y la unidad son los de la necesidad (así suma a su barra)
            Articulo = necesidad?.Nombre ?? model.Articulo.Trim(),
            Categoria = necesidad?.Categoria ?? model.Categoria!.Value,
            Unidad = necesidad?.Unidad ?? model.Unidad.Trim(),
            CantidadOfrecida = model.Cantidad!.Value,
            FormaEntrega = model.FormaEntrega!.Value,
            FechaEntregaEstimada = model.FechaEntrega!.Value.Date,
            DireccionRecogida = model.FormaEntrega == FormaEntregaEspecie.Domicilio ? model.DireccionRecogida!.Trim() : null,
            Mensaje = string.IsNullOrWhiteSpace(model.Mensaje) ? null : model.Mensaje.Trim()
        };
        _db.DonacionesEspecie.Add(donacion);
        await _db.SaveChangesAsync();
        donacion.Codigo = $"ESP-{DateTime.UtcNow:yyyy}-{donacion.Id:D6}";

        await _notificaciones.AgregarAAdministradoresAsync(esal.Id, "Nueva donación en especie",
            $"Te ofrecieron {donacion.CantidadOfrecida} {donacion.Unidad} de {donacion.Articulo} para el {Formatos.Fecha(donacion.FechaEntregaEstimada)}" +
            (donacion.FormaEntrega == FormaEntregaEspecie.Domicilio ? " (hay que recogerla)." : "."),
            $"/Fundacion/Especie/Detalle/{donacion.Id}", "bi-box-seam");
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"¡Gracias! Registramos tu donación {donacion.Codigo}. Cuando {esal.Nombre} confirme que la recibió, te llegará el comprobante.";
        return RedirectToAction(nameof(MiDonacion), new { codigo = donacion.Codigo });
    }

    // ---------- El donante sigue su donación ----------

    [Authorize]
    [HttpGet("mis-donaciones/especie/{codigo}")]
    public async Task<IActionResult> MiDonacion(string codigo)
    {
        var d = await BuscarPropiaAsync(codigo);
        if (d is null) return NotFound();
        return View(Item(d));
    }

    [Authorize]
    [HttpPost("mis-donaciones/especie/{codigo}/cancelar")]
    public async Task<IActionResult> Cancelar(string codigo, string? motivo)
    {
        var d = await _db.DonacionesEspecie.IgnoreQueryFilters().Include(x => x.Esal)
            .FirstOrDefaultAsync(x => x.Codigo == codigo && x.DonanteId == UsuarioId);
        if (d is null) return NotFound();
        if (!d.Pendiente)
        {
            TempData["Error"] = "Solo puedes cancelar una donación que la fundación todavía no ha recibido.";
            return RedirectToAction(nameof(MiDonacion), new { codigo });
        }

        d.Estado = EstadoDonacionEspecie.Cancelada;
        d.MotivoNoRecibida = string.IsNullOrWhiteSpace(motivo) ? "El donante la canceló." : motivo.Trim()[..Math.Min(300, motivo.Trim().Length)];
        await _notificaciones.AgregarAAdministradoresAsync(d.EsalId, "Cancelaron una donación en especie",
            $"El donante canceló {d.Codigo} ({d.CantidadOfrecida} {d.Unidad} de {d.Articulo}).", $"/Fundacion/Especie/Detalle/{d.Id}", "bi-box-seam");
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"Cancelaste la donación {d.Codigo}. Avisamos a {d.Esal!.Nombre}.";
        return RedirectToAction(nameof(MiDonacion), new { codigo });
    }

    [Authorize]
    [HttpGet("mis-donaciones/especie/{codigo}/comprobante")]
    public async Task<IActionResult> Comprobante(string codigo)
    {
        var d = await BuscarPropiaAsync(codigo);
        if (d is null || d.Estado != EstadoDonacionEspecie.Recibida) return NotFound();

        var donante = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == d.DonanteId);
        var pdf = _comprobantes.GenerarComprobanteEspecie(new DatosComprobanteEspecie(
            d.Codigo, donante.NombreCompleto, donante.Email ?? "", d.Esal!.Nombre, d.Esal.Nit,
            d.Articulo, Textos.De(d.Categoria), d.CantidadRecibida ?? d.CantidadOfrecida, d.Unidad,
            d.FechaRecepcion ?? DateTime.UtcNow, d.FechaVencimiento, d.ObservacionesRecepcion));
        return File(pdf, "application/pdf", $"Comprobante-{d.Codigo}.pdf");
    }

    // ---------- Apoyo ----------

    /// <summary>Lo recibido en los últimos 12 meses, agrupado por tipo (sirve para el perfil y la veeduría).</summary>
    private async Task<IReadOnlyList<RecibidoPorCategoria>> RecibidoUltimoAnioAsync(int esalId)
    {
        var desde = DateTime.UtcNow.AddYears(-1);
        var filas = await _db.DonacionesEspecie.IgnoreQueryFilters().AsNoTracking()
            .Where(d => d.EsalId == esalId && d.Estado == EstadoDonacionEspecie.Recibida && d.FechaRecepcion >= desde)
            .Select(d => new { d.Categoria, d.Unidad, Cantidad = d.CantidadRecibida ?? 0 })
            .ToListAsync();
        return filas.GroupBy(f => f.Categoria)
            .Select(g => new RecibidoPorCategoria(g.Key, g.Count(),
                string.Join(", ", g.GroupBy(x => x.Unidad).Select(u => $"{u.Sum(x => x.Cantidad)} {u.Key}"))))
            .OrderByDescending(r => r.Donaciones)
            .ToList();
    }

    private OfrecerEspecieViewModel Preparar(OfrecerEspecieViewModel model, Esal esal, NecesidadEspecie? necesidad = null)
    {
        model.Slug = esal.Slug!;
        model.NombreEsal = esal.Nombre;
        model.LogoUrl = UrlArchivo(esal.LogoRuta);
        if (necesidad is not null)
        {
            model.NombreNecesidad = necesidad.Nombre;
            model.Articulo = necesidad.Nombre;
            model.Categoria = necesidad.Categoria;
            model.Unidad = necesidad.Unidad;
        }
        return model;
    }

    private MiDonacionEspecieItem Item(DonacionEspecie d) => new()
    {
        Codigo = d.Codigo,
        NombreEsal = d.Esal!.Nombre,
        SlugEsal = d.Esal.Slug,
        LogoUrl = UrlArchivo(d.Esal.LogoRuta),
        Articulo = d.Articulo,
        Categoria = d.Categoria,
        CantidadOfrecida = d.CantidadOfrecida,
        CantidadRecibida = d.CantidadRecibida,
        Unidad = d.Unidad,
        FormaEntrega = d.FormaEntrega,
        FechaEntregaEstimada = d.FechaEntregaEstimada,
        FechaOferta = d.FechaOferta,
        Estado = d.Estado,
        FechaRecepcion = d.FechaRecepcion,
        MotivoNoRecibida = d.MotivoNoRecibida,
        FotoUrl = UrlArchivo(d.FotoRecepcionRuta),
        DireccionEsal = string.Join(" · ", new[] { d.Esal.Ciudad, d.Esal.Telefono }.Where(x => !string.IsNullOrWhiteSpace(x)))
    };

    private IActionResult CuentaInstitucional(string slug)
    {
        TempData["Error"] = Politicas.MensajeCuentaInstitucional;
        return RedirectToAction(nameof(Necesidades), new { slug });
    }

    private Task<Esal?> BuscarAsync(string slug)
        => _db.Esales.AsNoTracking().FirstOrDefaultAsync(e => e.Slug == slug && e.Activa);

    private Task<NecesidadEspecie?> BuscarNecesidadAsync(int esalId, int id)
        => _db.NecesidadesEspecie.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(n => n.Id == id && n.EsalId == esalId && n.Activa);

    private Task<DonacionEspecie?> BuscarPropiaAsync(string codigo)
        => _db.DonacionesEspecie.IgnoreQueryFilters().AsNoTracking().Include(d => d.Esal)
            .FirstOrDefaultAsync(d => d.Codigo == codigo && d.DonanteId == UsuarioId);

    private string? UrlArchivo(string? clave) => string.IsNullOrEmpty(clave) ? null : _archivos.UrlPublica(clave);
}
