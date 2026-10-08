using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Constantes;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Areas.Fundacion.Models;
using MunerApp.Web.Servicios;
using MunerApp.Web.Validacion;

namespace MunerApp.Web.Areas.Fundacion.Controllers;

/// <summary>
/// HU-015: la fundación confirma o rechaza las donaciones reportadas.
/// Pueden hacerlo los administradores principal y de consulta. El filtro global por ESAL
/// garantiza que solo vean las donaciones de su fundación.
/// </summary>
[Area("Fundacion")]
[Authorize(Roles = Roles.AdministradorESAL)]
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

    [HttpGet]
    public async Task<IActionResult> Index(EstadoDonacion estado = EstadoDonacion.Pendiente)
    {
        var conteos = await _db.Donaciones.AsNoTracking()
            .GroupBy(d => d.Estado)
            .Select(g => new { g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Cantidad);

        var donaciones = await (from d in _db.Donaciones.AsNoTracking()
                                join u in _db.Users on d.DonanteId equals u.Id
                                where d.Estado == estado
                                orderby d.FechaReporte descending
                                select new DonacionEsalItem
                                {
                                    Id = d.Id,
                                    Codigo = d.Codigo,
                                    Donante = u.NombreCompleto,
                                    CorreoDonante = u.Email ?? "",
                                    Valor = d.Valor,
                                    FechaTransferencia = d.FechaTransferencia,
                                    FechaReporte = d.FechaReporte,
                                    Estado = d.Estado,
                                    Apadrinado = d.Apadrinamiento != null ? d.Apadrinamiento.Beneficiario!.Nombre : null
                                }).Take(200).ToListAsync();

        return View(new DonacionesEsalViewModel
        {
            Estado = estado,
            Conteos = conteos,
            Donaciones = donaciones,
            TotalConfirmado = await _db.Donaciones.Where(d => d.Estado == EstadoDonacion.Confirmada).SumAsync(d => (decimal?)d.Valor) ?? 0
        });
    }

    [HttpGet]
    public async Task<IActionResult> Detalle(int id)
    {
        var d = await _db.Donaciones.AsNoTracking().Include(x => x.Apadrinamiento!).ThenInclude(a => a.Beneficiario).FirstOrDefaultAsync(x => x.Id == id);
        if (d is null) return NotFound();

        var donante = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == d.DonanteId);
        var revisor = d.RevisadoPorId is null ? null
            : await _db.Users.AsNoTracking().Where(u => u.Id == d.RevisadoPorId).Select(u => u.NombreCompleto).FirstOrDefaultAsync();

        return View(new DonacionEsalDetalleViewModel
        {
            Id = d.Id,
            Codigo = d.Codigo,
            Donante = donante.NombreCompleto,
            CorreoDonante = donante.Email ?? "",
            Valor = d.Valor,
            FechaTransferencia = d.FechaTransferencia,
            FechaReporte = d.FechaReporte,
            Estado = d.Estado,
            Apadrinado = d.Apadrinamiento?.Beneficiario?.Nombre,
            MedioPago = d.MedioPago,
            ReferenciaPago = d.ReferenciaPago,
            Mensaje = d.Mensaje,
            MotivoRechazo = d.MotivoRechazo,
            FechaRevision = d.FechaRevision,
            RevisadoPor = revisor,
            SoporteEsPdf = d.SoporteRuta.EndsWith(".pdf")
        });
    }

    [HttpGet]
    public async Task<IActionResult> Soporte(int id)
    {
        var d = await _db.Donaciones.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (d is null) return NotFound();
        var stream = await _archivos.AbrirAsync(d.SoporteRuta);
        return stream is null ? NotFound() : File(stream, ValidadorArchivos.ContentTypeDe(d.SoporteRuta));
    }

    // Escenario 1: confirmar
    [HttpPost]
    public async Task<IActionResult> Confirmar(int id)
    {
        var d = await _db.Donaciones.Include(x => x.Esal).FirstOrDefaultAsync(x => x.Id == id);
        if (d is null) return NotFound();
        if (d.Estado != EstadoDonacion.Pendiente)
        {
            TempData["Error"] = $"La donación {d.Codigo} ya había sido {(d.Estado == EstadoDonacion.Confirmada ? "confirmada" : "rechazada")}.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        d.Estado = EstadoDonacion.Confirmada;
        d.FechaRevision = DateTime.UtcNow;
        d.RevisadoPorId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        // Escenario 3: notificación al donante
        _notificaciones.Agregar(d.DonanteId, "¡Tu donación fue confirmada!",
            $"{d.Esal!.Nombre} confirmó tu donación {d.Codigo} por {d.Valor:C0}. Ya puedes descargar tu comprobante.",
            $"/mis-donaciones/{d.Codigo}", "bi-check-circle");
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"Confirmaste la donación {d.Codigo}. El donante ya puede descargar su comprobante.";
        return RedirectToAction(nameof(Index));
    }

    // Escenario 2: rechazar con motivo
    [HttpPost]
    public async Task<IActionResult> Rechazar(int id, string? motivo)
    {
        var d = await _db.Donaciones.Include(x => x.Esal).FirstOrDefaultAsync(x => x.Id == id);
        if (d is null) return NotFound();
        if (d.Estado != EstadoDonacion.Pendiente)
        {
            TempData["Error"] = $"La donación {d.Codigo} ya había sido revisada.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        motivo = motivo?.Trim();
        if (string.IsNullOrEmpty(motivo) || motivo.Length < 10)
        {
            TempData["Error"] = "Escribe el motivo del rechazo (mínimo 10 caracteres) para que el donante sepa qué pasó.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        d.Estado = EstadoDonacion.Rechazada;
        d.MotivoRechazo = motivo.Length > 300 ? motivo[..300] : motivo;
        d.FechaRevision = DateTime.UtcNow;
        d.RevisadoPorId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        _notificaciones.Agregar(d.DonanteId, "Tu donación no pudo confirmarse",
            $"{d.Esal!.Nombre} no confirmó tu donación {d.Codigo}. Motivo: {d.MotivoRechazo}",
            $"/mis-donaciones/{d.Codigo}", "bi-x-circle");
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"Rechazaste la donación {d.Codigo}. El donante verá el motivo.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Comprobante(int id)
    {
        var d = await _db.Donaciones.AsNoTracking().Include(x => x.Esal).FirstOrDefaultAsync(x => x.Id == id);
        if (d is null || d.Estado != EstadoDonacion.Confirmada) return NotFound();

        var donante = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == d.DonanteId);
        return File(_comprobantes.GenerarComprobanteDonacion(Comprobantes.Datos(d, donante)), "application/pdf", $"Comprobante-{d.Codigo}.pdf");
    }
}
