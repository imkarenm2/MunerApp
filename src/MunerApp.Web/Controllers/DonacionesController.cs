using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Application.Seguridad;
using MunerApp.Domain.Constantes;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Identity;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Models.Publico;
using MunerApp.Web.Servicios;
using MunerApp.Web.Validacion;

namespace MunerApp.Web.Controllers;

/// <summary>
/// HU-014: el donante reporta una donación hecha por llave o transferencia y consulta "Mis donaciones".
/// Las donaciones de un donante pueden ser de varias fundaciones: por eso se consultan con
/// IgnoreQueryFilters y siempre filtrando por el usuario autenticado.
/// </summary>
[Authorize]
public class DonacionesController : Controller
{
    private readonly MunerAppDbContext _db;
    private readonly IAlmacenamientoArchivos _archivos;
    private readonly INotificacionService _notificaciones;
    private readonly IComprobanteService _comprobantes;

    public DonacionesController(MunerAppDbContext db, IAlmacenamientoArchivos archivos,
        INotificacionService notificaciones, IComprobanteService comprobantes)
    {
        _db = db;
        _archivos = archivos;
        _notificaciones = notificaciones;
        _comprobantes = comprobantes;
    }

    private string UsuarioId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // ---------- Reportar donación ----------

    [HttpGet("fundaciones/{slug}/reportar-donacion")]
    public async Task<IActionResult> Reportar(string slug)
    {
        var (esal, datos) = await BuscarAsync(slug);
        if (esal is null) return NotFound();
        if (datos is null)
        {
            TempData["Error"] = "Esta fundación todavía no tiene datos para recibir donaciones.";
            return Redirect($"/fundaciones/{slug}/donar");
        }

        return View(Preparar(new ReportarDonacionViewModel { FechaTransferencia = DateTime.Today }, esal, datos));
    }

    [HttpPost("fundaciones/{slug}/reportar-donacion")]
    [RequestSizeLimit(ValidadorArchivos.LimitePeticionBytes)]
    public async Task<IActionResult> Reportar(string slug, ReportarDonacionViewModel model)
    {
        var (esal, datos) = await BuscarAsync(slug);
        if (esal is null) return NotFound();
        if (datos is null) return Redirect($"/fundaciones/{slug}/donar");

        var valor = LeerValor(model.Valor);
        if (!string.IsNullOrWhiteSpace(model.Valor) && valor is null)
            ModelState.AddModelError(nameof(model.Valor), "Escribe el valor solo con números, por ejemplo 50.000.");
        else if (valor is < 1000)
            ModelState.AddModelError(nameof(model.Valor), "El valor mínimo es $1.000.");
        else if (valor is > 100_000_000)
            ModelState.AddModelError(nameof(model.Valor), "Para donaciones de más de $100.000.000 comunícate directamente con la fundación.");

        if (model.FechaTransferencia is DateTime fecha)
        {
            if (fecha.Date > DateTime.Today.AddDays(1))
                ModelState.AddModelError(nameof(model.FechaTransferencia), "La fecha no puede ser futura.");
            else if (fecha.Date < DateTime.Today.AddDays(-90))
                ModelState.AddModelError(nameof(model.FechaTransferencia), "Solo puedes reportar transferencias de los últimos 90 días.");
        }

        // Escenario 2: sin soporte no se puede enviar
        ArchivoValidado? soporte = null;
        if (model.Soporte is null || model.Soporte.Length == 0)
            ModelState.AddModelError(nameof(model.Soporte), "Adjunta el soporte de la transferencia (captura o PDF) para poder reportarla.");
        else
        {
            soporte = await ValidadorArchivos.ValidarAsync(model.Soporte, TipoArchivo.Documento);
            if (!soporte.Valido) ModelState.AddModelError(nameof(model.Soporte), soporte.Error!);
        }

        if (!ModelState.IsValid) return View(Preparar(model, esal, datos));

        string clave;
        await using (var stream = model.Soporte!.OpenReadStream())
            clave = await _archivos.GuardarAsync(stream, $"esal/{esal.Id}/donaciones", soporte!.Extension, publico: false);

        // Escenario 1: queda pendiente con un código único
        await using var transaccion = await _db.Database.BeginTransactionAsync();
        var donacion = new Donacion
        {
            EsalId = esal.Id,
            Codigo = "TMP-" + Guid.NewGuid().ToString("N")[..16],
            DonanteId = UsuarioId,
            Valor = valor!.Value,
            FechaTransferencia = model.FechaTransferencia!.Value.Date,
            MedioPago = $"{datos.Entidad} · {Textos.De(datos.TipoCuenta, datos.TipoLlave)} {datos.Numero}",
            ReferenciaPago = model.ReferenciaPago?.Trim(),
            Mensaje = model.Mensaje?.Trim(),
            SoporteRuta = clave
        };
        _db.Donaciones.Add(donacion);
        await _db.SaveChangesAsync();

        donacion.Codigo = $"DON-{DateTime.UtcNow:yyyy}-{donacion.Id:D6}";
        await _notificaciones.AgregarAAdministradoresAsync(esal.Id,
            "Nueva donación por confirmar",
            $"{User.FindFirstValue(MunerAppClaims.NombreCompleto) ?? "Un donante"} reportó una donación de {valor:C0}. Revisa el soporte y confírmala.",
            $"/Fundacion/Donaciones/Detalle/{donacion.Id}", "bi-cash-coin");
        await _db.SaveChangesAsync();
        await transaccion.CommitAsync();

        TempData["Mensaje"] = $"¡Gracias! Reportaste tu donación con el código {donacion.Codigo}. Te avisaremos cuando {esal.Nombre} la confirme.";
        return RedirectToAction(nameof(Detalle), new { codigo = donacion.Codigo });
    }

    // ---------- Escenario 3: historial ----------

    [HttpGet("mis-donaciones")]
    public async Task<IActionResult> Index()
    {
        var donaciones = await _db.Donaciones.IgnoreQueryFilters().AsNoTracking()
            .Where(d => d.DonanteId == UsuarioId)
            .OrderByDescending(d => d.FechaReporte)
            .Select(d => new { d.Codigo, d.Esal!.Nombre, d.Esal.Slug, d.Esal.LogoRuta, d.Valor, d.FechaTransferencia, d.FechaReporte, d.Estado })
            .ToListAsync();

        return View(donaciones.Select(d => new DonacionItem
        {
            Codigo = d.Codigo,
            NombreEsal = d.Nombre,
            SlugEsal = d.Slug,
            LogoUrl = d.LogoRuta is null ? null : _archivos.UrlPublica(d.LogoRuta),
            Valor = d.Valor,
            FechaTransferencia = d.FechaTransferencia,
            FechaReporte = d.FechaReporte,
            Estado = d.Estado
        }).ToList());
    }

    [HttpGet("mis-donaciones/{codigo}")]
    public async Task<IActionResult> Detalle(string codigo)
    {
        var d = await BuscarPropiaAsync(codigo);
        if (d is null) return NotFound();

        return View(new DonacionDetalleViewModel
        {
            Codigo = d.Codigo,
            NombreEsal = d.Esal!.Nombre,
            SlugEsal = d.Esal.Slug,
            LogoUrl = d.Esal.LogoRuta is null ? null : _archivos.UrlPublica(d.Esal.LogoRuta),
            Valor = d.Valor,
            FechaTransferencia = d.FechaTransferencia,
            FechaReporte = d.FechaReporte,
            Estado = d.Estado,
            MedioPago = d.MedioPago,
            ReferenciaPago = d.ReferenciaPago,
            Mensaje = d.Mensaje,
            MotivoRechazo = d.MotivoRechazo,
            FechaRevision = d.FechaRevision,
            SoporteEsPdf = d.SoporteRuta.EndsWith(".pdf")
        });
    }

    [HttpGet("mis-donaciones/{codigo}/soporte")]
    public async Task<IActionResult> Soporte(string codigo)
    {
        var d = await BuscarPropiaAsync(codigo);
        if (d is null) return NotFound();
        var stream = await _archivos.AbrirAsync(d.SoporteRuta);
        if (stream is null) return NotFound();
        return File(stream, ValidadorArchivos.ContentTypeDe(d.SoporteRuta));
    }

    /// <summary>HU-015: comprobante en PDF de una donación confirmada.</summary>
    [HttpGet("mis-donaciones/{codigo}/comprobante")]
    public async Task<IActionResult> Comprobante(string codigo)
    {
        var d = await BuscarPropiaAsync(codigo);
        if (d is null || d.Estado != EstadoDonacion.Confirmada) return NotFound();

        var donante = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == d.DonanteId);
        var pdf = _comprobantes.GenerarComprobanteDonacion(Comprobantes.Datos(d, donante));
        return File(pdf, "application/pdf", $"Comprobante-{d.Codigo}.pdf");
    }

    // ---------- Auxiliares ----------

    private async Task<(Esal? Esal, DatosDonacion? Datos)> BuscarAsync(string slug)
    {
        var esal = await _db.Esales.AsNoTracking().FirstOrDefaultAsync(e => e.Slug == slug && e.Activa);
        if (esal is null) return (null, null);
        var datos = await _db.DatosDonacion.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(d => d.EsalId == esal.Id);
        return (esal, datos);
    }

    private Task<Donacion?> BuscarPropiaAsync(string codigo)
        => _db.Donaciones.IgnoreQueryFilters().AsNoTracking().Include(d => d.Esal)
            .FirstOrDefaultAsync(d => d.Codigo == codigo && d.DonanteId == UsuarioId);

    private ReportarDonacionViewModel Preparar(ReportarDonacionViewModel model, Esal esal, DatosDonacion datos)
    {
        model.Slug = esal.Slug!;
        model.NombreEsal = esal.Nombre;
        model.LogoUrl = esal.LogoRuta is null ? null : _archivos.UrlPublica(esal.LogoRuta);
        model.MedioPago = $"{datos.Entidad} · {Textos.De(datos.TipoCuenta, datos.TipoLlave)}: {datos.Numero}";
        return model;
    }

    /// <summary>Acepta "50000", "50.000", "$ 50.000" o "50,000" (pesos sin decimales).</summary>
    private static decimal? LeerValor(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var limpio = texto.Replace("$", "").Replace("COP", "", StringComparison.OrdinalIgnoreCase).Trim();
        if (limpio.Any(c => !char.IsDigit(c) && c is not '.' and not ',' and not ' ')) return null;
        var digitos = new string(limpio.Where(char.IsDigit).ToArray());
        return digitos.Length is > 0 and <= 12 ? decimal.Parse(digitos) : null;
    }
}
