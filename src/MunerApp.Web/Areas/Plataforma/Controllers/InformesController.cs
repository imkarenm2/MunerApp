using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MunerApp.Domain.Constantes;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Areas.Plataforma.Models;
using MunerApp.Web.Servicios;

namespace MunerApp.Web.Areas.Plataforma.Controllers;

/// <summary>
/// Informes de seguimiento para el superadministrador: cuánto se mueve en la plataforma y en cada fundación.
/// No muestran quién dona ni quién apadrina; los datos personales los ve solo la fundación que recibe el aporte.
/// </summary>
[Area("Plataforma")]
[Authorize(Roles = Roles.SuperAdministrador)]
public class InformesController : Controller
{
    private const int Meses = 6;
    private static readonly CultureInfo Co = new("es-CO");
    private readonly MunerAppDbContext _db;

    public InformesController(MunerAppDbContext db) => _db = db;

    [HttpGet]
    public IActionResult Index() => RedirectToAction(nameof(Donaciones));

    [HttpGet]
    public async Task<IActionResult> Donaciones(int? esal)
    {
        var donaciones = _db.Donaciones.AsNoTracking().AsQueryable();
        if (esal is int id) donaciones = donaciones.Where(d => d.EsalId == id);

        var confirmadas = donaciones.Where(d => d.Estado == EstadoDonacion.Confirmada);
        var pendientes = donaciones.Where(d => d.Estado == EstadoDonacion.Pendiente);

        var modelo = new InformeDonacionesViewModel
        {
            EsalId = esal,
            Fundaciones = await FundacionesAsync(esal),
            Confirmado = await confirmadas.SumAsync(d => (decimal?)d.Valor) ?? 0,
            CantidadConfirmadas = await confirmadas.CountAsync(),
            Pendiente = await pendientes.SumAsync(d => (decimal?)d.Valor) ?? 0,
            CantidadPendientes = await pendientes.CountAsync(),
            CantidadRechazadas = await donaciones.CountAsync(d => d.Estado == EstadoDonacion.Rechazada),
            DonantesDistintos = await confirmadas.Select(d => d.DonanteId).Distinct().CountAsync()
        };

        // Días promedio entre el reporte y la revisión (se calcula en memoria: son pocas filas por fundación)
        var revisiones = await donaciones.Where(d => d.FechaRevision != null)
            .Select(d => new { d.FechaReporte, d.FechaRevision }).ToListAsync();
        if (revisiones.Count > 0)
            modelo.DiasPromedioRevision = Math.Round(revisiones.Average(r => (r.FechaRevision!.Value - r.FechaReporte).TotalDays), 1);

        // Confirmado por mes (según la fecha en que la fundación lo confirmó)
        var desde = InicioMes(Meses - 1);
        var porMes = await confirmadas.Where(d => d.FechaRevision >= desde)
            .Select(d => new { Fecha = d.FechaRevision!.Value, d.Valor }).ToListAsync();
        modelo.PorMes = Enumerable.Range(0, Meses).Reverse().Select(i =>
        {
            var inicio = Formatos.Local(InicioMes(i));
            var delMes = porMes.Where(d => { var l = Formatos.Local(d.Fecha); return l.Year == inicio.Year && l.Month == inicio.Month; }).ToList();
            return new BarraMes(inicio.ToString("MMM yyyy", Co), delMes.Sum(d => d.Valor), delMes.Count);
        }).ToList();

        // A qué se destina el dinero
        var destinos = await confirmadas
            .GroupBy(d => d.CausaId != null ? 1 : d.ApadrinamientoId != null ? 2 : 0)
            .Select(g => new { g.Key, Valor = g.Sum(d => d.Valor), N = g.Count() })
            .ToListAsync();
        string NombreDestino(int k) => k switch { 1 => "Causas", 2 => "Apadrinamientos", _ => "Donación general" };
        modelo.PorDestino = destinos.OrderByDescending(d => d.Valor).Select(d => new FilaTotal(NombreDestino(d.Key), d.Valor, d.N)).ToList();

        modelo.PorFundacion = (await confirmadas
            .GroupBy(d => d.Esal!.Nombre)
            .Select(g => new { g.Key, Valor = g.Sum(d => d.Valor), N = g.Count() })
            .ToListAsync())
            .OrderByDescending(f => f.Valor)
            .Select(f => new FilaTotal(f.Key, f.Valor, f.N)).ToList();

        modelo.Recientes = (await donaciones
            .OrderByDescending(d => d.FechaReporte).Take(20)
            .Select(d => new
            {
                d.Codigo, Fundacion = d.Esal!.Nombre, d.Valor, d.Estado, d.FechaReporte,
                Causa = d.Causa != null ? d.Causa.Titulo : null,
                Apadrinado = d.Apadrinamiento != null ? d.Apadrinamiento.Beneficiario!.Nombre : null
            }).ToListAsync())
            .Select(d => new DonacionInformeItem(d.Codigo, d.Fundacion,
                d.Causa is not null ? $"Causa: {d.Causa}" : d.Apadrinado is not null ? $"Apadrinamiento de {d.Apadrinado}" : "Donación general",
                d.Valor, d.Estado, d.FechaReporte))
            .ToList();

        return View(modelo);
    }

    [HttpGet]
    public async Task<IActionResult> Apadrinamientos(int? esal)
    {
        var todos = _db.Apadrinamientos.AsNoTracking().AsQueryable();
        var beneficiarios = _db.Beneficiarios.AsNoTracking().AsQueryable();
        var donaciones = _db.Donaciones.AsNoTracking().AsQueryable();
        if (esal is int id)
        {
            todos = todos.Where(a => a.EsalId == id);
            beneficiarios = beneficiarios.Where(b => b.EsalId == id);
            donaciones = donaciones.Where(d => d.EsalId == id);
        }
        var activos = todos.Where(a => a.Estado == EstadoApadrinamiento.Activo);
        var enCasa = ReglasBeneficiario.EnCasa;

        var hoy = Formatos.Hoy();
        var inicioMesUtc = Formatos.AUtc(new DateTime(hoy.Year, hoy.Month, 1));

        var modelo = new InformeApadrinamientosViewModel
        {
            EsalId = esal,
            Fundaciones = await FundacionesAsync(esal),
            Activos = await activos.CountAsync(),
            Cancelados = await todos.CountAsync(a => a.Estado == EstadoApadrinamiento.Cancelado),
            PadrinosDistintos = await activos.Select(a => a.PadrinoId).Distinct().CountAsync(),
            GatosApadrinados = await activos.Select(a => a.BeneficiarioId).Distinct().CountAsync(),
            GatosApadrinables = await beneficiarios.CountAsync(b => b.Apadrinable && enCasa.Contains(b.Estado)),
            AporteMensual = await activos.SumAsync(a => (decimal?)a.ValorMensual) ?? 0,
            AporteRecibidoMes = await donaciones
                .Where(d => d.ApadrinamientoId != null && d.Estado == EstadoDonacion.Confirmada && d.FechaRevision >= inicioMesUtc)
                .SumAsync(d => (decimal?)d.Valor) ?? 0
        };

        var desde = InicioMes(Meses - 1);
        var nuevos = await todos.Where(a => a.FechaInicio >= desde).Select(a => new { a.FechaInicio, a.ValorMensual }).ToListAsync();
        modelo.NuevosPorMes = Enumerable.Range(0, Meses).Reverse().Select(i =>
        {
            var inicio = Formatos.Local(InicioMes(i));
            var delMes = nuevos.Where(a => { var l = Formatos.Local(a.FechaInicio); return l.Year == inicio.Year && l.Month == inicio.Month; }).ToList();
            return new BarraMes(inicio.ToString("MMM yyyy", Co), delMes.Sum(a => a.ValorMensual), delMes.Count);
        }).ToList();

        var porFundacion = await activos.GroupBy(a => a.EsalId)
            .Select(g => new { g.Key, N = g.Count(), V = g.Sum(a => a.ValorMensual) })
            .ToDictionaryAsync(x => x.Key, x => (x.N, x.V));
        var apadrinables = await beneficiarios.Where(b => b.Apadrinable && enCasa.Contains(b.Estado))
            .GroupBy(b => b.EsalId).Select(g => new { g.Key, N = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.N);
        var nombres = await _db.Esales.AsNoTracking().Select(e => new { e.Id, e.Nombre }).ToDictionaryAsync(e => e.Id, e => e.Nombre);
        modelo.PorFundacion = porFundacion.Keys.Union(apadrinables.Keys)
            .Select(k => new FilaApadrinamientoFundacion(nombres.GetValueOrDefault(k) ?? "—",
                porFundacion.TryGetValue(k, out var p) ? p.N : 0,
                apadrinables.GetValueOrDefault(k),
                porFundacion.TryGetValue(k, out var q) ? q.V : 0))
            .OrderByDescending(f => f.AporteMensual).ThenBy(f => f.Fundacion)
            .ToList();

        modelo.Recientes = (await todos.OrderByDescending(a => a.FechaInicio).Take(15)
            .Select(a => new { Fundacion = a.Esal!.Nombre, Gato = a.Beneficiario!.Nombre, a.ValorMensual, a.Estado, a.FechaInicio })
            .ToListAsync())
            .Select(a => new ApadrinamientoInformeItem(a.Fundacion, a.Gato, a.ValorMensual, a.Estado, a.FechaInicio))
            .ToList();

        return View(modelo);
    }

    /// <summary>Inicio (en UTC) del mes de hace <paramref name="mesesAtras"/> meses, en hora de Colombia.</summary>
    private static DateTime InicioMes(int mesesAtras)
    {
        var hoy = Formatos.Hoy();
        return Formatos.AUtc(new DateTime(hoy.Year, hoy.Month, 1).AddMonths(-mesesAtras));
    }

    private async Task<IReadOnlyList<SelectListItem>> FundacionesAsync(int? seleccionada)
        => await _db.Esales.AsNoTracking().OrderBy(e => e.Nombre)
            .Select(e => new SelectListItem(e.Nombre, e.Id.ToString(), e.Id == seleccionada))
            .ToListAsync();
}
