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
using MunerApp.Web.Seguridad;
using MunerApp.Web.Servicios;

namespace MunerApp.Web.Areas.Fundacion.Controllers;

/// <summary>
/// HU-037: inventario clínico de medicamentos e insumos.
/// Escenario 1: se registra con sus datos y su cantidad inicial. Escenario 2: la cantidad solo cambia con
/// entradas y usos, que quedan en el historial de movimientos. Escenario 3: solo los administradores y los
/// voluntarios de salud tienen acceso; el voluntario general no. Requiere el módulo de salud activo.
/// </summary>
[Area("Fundacion")]
[Authorize(Policy = Politicas.AccesoClinico)]
[RequiereModulo(CodigosModulo.Salud)]
public class MedicamentosController : Controller
{
    private const decimal CantidadMaxima = InventarioMedicamentos.CantidadMaxima;

    private readonly MunerAppDbContext _db;
    private readonly IEsalActual _esalActual;
    private readonly IReportesService _reportes;
    private readonly InventarioMedicamentos _inventario;
    private readonly AlertasSalud _alertas;

    public MedicamentosController(MunerAppDbContext db, IEsalActual esalActual, IReportesService reportes,
        InventarioMedicamentos inventario, AlertasSalud alertas)
    {
        _db = db;
        _esalActual = esalActual;
        _reportes = reportes;
        _inventario = inventario;
        _alertas = alertas;
    }

    private string UsuarioId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private int EsalId => _esalActual.EsalId ?? throw new InvalidOperationException("El usuario no pertenece a una ESAL.");

    // ---------- HU-040: reporte de medicamentos (solo administradores) ----------

    /// <summary>Escenario 1: reporte completo. Escenario 2: filtro por vencidos, por vencer o stock bajo.</summary>
    [HttpGet]
    [Authorize(Roles = Roles.AdministradorESAL)]
    public async Task<IActionResult> Reporte(FiltroReporteMedicamentos filtro = FiltroReporteMedicamentos.Todos)
        => View(await ArmarReporteAsync(filtro));

    /// <summary>Escenario 3: el mismo reporte (con el filtro elegido) en PDF.</summary>
    [HttpGet]
    [Authorize(Roles = Roles.AdministradorESAL)]
    public async Task<IActionResult> ReportePdf(FiltroReporteMedicamentos filtro = FiltroReporteMedicamentos.Todos)
    {
        var reporte = await ArmarReporteAsync(filtro);
        var esal = await _db.Esales.AsNoTracking().Where(e => e.Id == EsalId).Select(e => new { e.Nombre, e.Nit }).FirstAsync();
        var filas = reporte.Filas.Select(f => new FilaReporteMedicamento(
            f.NombreComercial, f.PrincipioActivo, f.FechaVencimiento, f.Uso, Textos.De(f.Via), f.Dosis,
            $"{Formatos.Cantidad(f.Cantidad)} {Textos.UnidadDe(f.Presentacion)}", f.Estado)).ToList();

        var pdf = _reportes.GenerarReporteMedicamentos(new DatosReporteMedicamentos(
            esal.Nombre, esal.Nit, ReporteMedicamentosViewModel.TextoDe(filtro), DateTime.UtcNow,
            filas, reporte.Vencidos, reporte.PorVencer, reporte.StockBajo));

        var hoy = Formatos.Local(DateTime.UtcNow);
        return File(pdf, "application/pdf", $"Reporte-medicamentos-{ReporteMedicamentosViewModel.TextoDe(filtro).Replace(' ', '-')}-{hoy:yyyy-MM-dd}.pdf");
    }

    private async Task<ReporteMedicamentosViewModel> ArmarReporteAsync(FiltroReporteMedicamentos filtro)
    {
        var hoy = Formatos.Local(DateTime.UtcNow).Date;
        var dias = await AlertasSalud.DiasAvisoAsync(_db, EsalId); // el mismo plazo de las alertas (HU-039)

        // El inventario de una fundación es pequeño: se clasifica en memoria con las mismas reglas del dominio
        var todos = await _db.Medicamentos.AsNoTracking().OrderBy(m => m.FechaVencimiento).ThenBy(m => m.NombreComercial).ToListAsync();
        var filas = todos.Select(m =>
        {
            var vencido = m.EstaVencido(hoy);
            var porVencer = m.EstaPorVencer(hoy, dias);
            var estados = new List<string>();
            if (vencido) estados.Add("Vencido");
            if (porVencer)
            {
                var faltan = (m.FechaVencimiento.Date - hoy).Days;
                estados.Add(faltan == 0 ? "Vence hoy" : $"Vence en {faltan} {(faltan == 1 ? "día" : "días")}");
            }
            if (m.TieneStockBajo) estados.Add("Stock bajo");
            return new FilaReporteMedicamentoItem
            {
                Id = m.Id,
                NombreComercial = m.NombreComercial,
                PrincipioActivo = m.PrincipioActivo,
                FechaVencimiento = m.FechaVencimiento,
                Cantidad = m.Cantidad,
                CantidadMinima = m.CantidadMinima,
                Presentacion = m.Presentacion,
                Via = m.Via,
                Uso = m.Uso,
                Dosis = m.Dosis,
                Vencido = vencido,
                PorVencer = porVencer,
                StockBajo = m.TieneStockBajo,
                Estado = estados.Count == 0 ? "Al día" : string.Join(" · ", estados)
            };
        }).ToList();

        return new ReporteMedicamentosViewModel
        {
            Filtro = filtro,
            DiasPorVencer = dias,
            Total = filas.Count,
            Vencidos = filas.Count(f => f.Vencido),
            PorVencer = filas.Count(f => f.PorVencer),
            StockBajo = filas.Count(f => f.StockBajo),
            Filas = filtro switch
            {
                FiltroReporteMedicamentos.Vencidos => filas.Where(f => f.Vencido).ToList(),
                FiltroReporteMedicamentos.PorVencer => filas.Where(f => f.PorVencer).ToList(),
                FiltroReporteMedicamentos.StockBajo => filas.Where(f => f.StockBajo).ToList(),
                _ => filas
            }
        };
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q)
    {
        var consulta = _db.Medicamentos.AsNoTracking();
        q = q?.Trim();
        if (!string.IsNullOrEmpty(q))
            consulta = consulta.Where(m => m.NombreComercial.Contains(q) || m.PrincipioActivo.Contains(q));

        var medicamentos = await consulta
            .OrderBy(m => m.FechaVencimiento).ThenBy(m => m.NombreComercial)
            .Select(m => new MedicamentoItem
            {
                Id = m.Id,
                NombreComercial = m.NombreComercial,
                PrincipioActivo = m.PrincipioActivo,
                FechaVencimiento = m.FechaVencimiento,
                Cantidad = m.Cantidad,
                CantidadMinima = m.CantidadMinima,
                Presentacion = m.Presentacion,
                Via = m.Via
            }).Take(300).ToListAsync();

        return View(new MedicamentosIndexViewModel
        {
            Busqueda = q,
            Medicamentos = medicamentos,
            DiasAviso = await AlertasSalud.DiasAvisoAsync(_db, EsalId)
        });
    }

    // ---------- HU-039: configuración de las alertas (administrador principal) ----------

    [HttpGet]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> Alertas()
    {
        var ultima = await _db.RevisionesDiarias.AsNoTracking()
            .Where(r => r.Tarea == _alertas.Nombre && r.Terminada != null)
            .OrderByDescending(r => r.Fecha).Select(r => r.Terminada).FirstOrDefaultAsync();
        return View(new AlertasSaludViewModel
        {
            DiasAvisoVencimiento = (await AlertasSalud.DiasAvisoAsync(_db, EsalId)).ToString(),
            UltimaRevision = ultima
        });
    }

    /// <summary>Escenario 1: el plazo de aviso de vencimiento lo define la fundación.</summary>
    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> Alertas(AlertasSaludViewModel model)
    {
        var dias = int.TryParse(model.DiasAvisoVencimiento?.Trim(), out var d) ? d : (int?)null;
        if (dias is not int valor || valor < ConfigSalud.DiasMinimos || valor > ConfigSalud.DiasMaximos)
        {
            ModelState.AddModelError(nameof(model.DiasAvisoVencimiento), $"Escribe un número de días entre {ConfigSalud.DiasMinimos} y {ConfigSalud.DiasMaximos}.");
            return View(model);
        }

        var config = await _db.ConfigSalud.FirstOrDefaultAsync();
        if (config is null)
            _db.ConfigSalud.Add(config = new ConfigSalud { EsalId = EsalId });
        config.DiasAvisoVencimiento = valor;
        config.FechaActualizacion = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"Listo: se avisará cuando un medicamento vaya a vencer en los próximos {valor} {(valor == 1 ? "día" : "días")}.";
        return RedirectToAction(nameof(Alertas));
    }

    /// <summary>
    /// Ejecuta ahora la revisión de esta fundación (la misma de todos los días), por ejemplo después de cambiar
    /// el plazo. No repite avisos ya dados.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> RevisarAhora()
    {
        var r = await _alertas.RevisarEsalAsync(EsalId, Formatos.Local(DateTime.UtcNow).Date);
        TempData["Mensaje"] = r.MedicamentosPorVencer == 0 && r.EventosManana == 0
            ? "Revisión hecha: no hay avisos nuevos. Lo que ya se había avisado no se repite."
            : $"Revisión hecha: {r.MedicamentosPorVencer} {(r.MedicamentosPorVencer == 1 ? "medicamento por vencer" : "medicamentos por vencer")} y " +
              $"{r.EventosManana} {(r.EventosManana == 1 ? "evento" : "eventos")} de mañana. El aviso llegó a las notificaciones del equipo de salud.";
        return RedirectToAction(nameof(Alertas));
    }

    // ---------- Escenario 1: registro ----------

    [HttpGet]
    public IActionResult Crear() => View("Formulario", new MedicamentoFormViewModel());

    [HttpPost]
    public async Task<IActionResult> Crear(MedicamentoFormViewModel model)
    {
        var cantidad = ValidarCantidad(model.Cantidad, nameof(model.Cantidad), obligatoria: true, permiteCero: true);
        var minima = ValidarCantidad(model.CantidadMinima, nameof(model.CantidadMinima), obligatoria: false, permiteCero: true);
        ValidarFecha(model);
        if (!ModelState.IsValid) return View("Formulario", model);

        var m = new Medicamento { EsalId = EsalId, RegistradoPorId = UsuarioId, Cantidad = cantidad!.Value };
        Copiar(model, m, minima);
        m.Movimientos.Add(new MovimientoMedicamento
        {
            EsalId = EsalId,
            Tipo = TipoMovimientoMedicamento.Registro,
            Cantidad = m.Cantidad,
            CantidadResultante = m.Cantidad,
            RegistradoPorId = UsuarioId
        });
        _db.Medicamentos.Add(m);
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"Registraste {m.NombreComercial} con {Formatos.Cantidad(m.Cantidad)} {Textos.UnidadDe(m.Presentacion)}.";
        return RedirectToAction(nameof(Detalle), new { id = m.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var m = await _db.Medicamentos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (m is null) return NotFound();
        return View("Formulario", new MedicamentoFormViewModel
        {
            Id = m.Id,
            NombreComercial = m.NombreComercial,
            PrincipioActivo = m.PrincipioActivo,
            FechaVencimiento = m.FechaVencimiento,
            Uso = m.Uso,
            Via = m.Via,
            Dosis = m.Dosis,
            Presentacion = m.Presentacion,
            CantidadMinima = m.CantidadMinima is decimal min ? Formatos.Cantidad(min) : null
        });
    }

    /// <summary>Edita los datos; la cantidad no se cambia aquí sino con entradas y usos (escenario 2).</summary>
    [HttpPost]
    public async Task<IActionResult> Editar(int id, MedicamentoFormViewModel model)
    {
        var m = await _db.Medicamentos.FirstOrDefaultAsync(x => x.Id == id);
        if (m is null) return NotFound();

        model.Id = id;
        var minima = ValidarCantidad(model.CantidadMinima, nameof(model.CantidadMinima), obligatoria: false, permiteCero: true);
        ValidarFecha(model);
        if (!ModelState.IsValid) return View("Formulario", model);

        Copiar(model, m, minima);
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"Guardaste los cambios de {m.NombreComercial}.";
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Detalle(int id)
    {
        var m = await _db.Medicamentos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (m is null) return NotFound();

        var movimientos = await (from mv in _db.MovimientosMedicamento.AsNoTracking()
                                 where mv.MedicamentoId == id
                                 join u in _db.Users on mv.RegistradoPorId equals u.Id into us
                                 from u in us.DefaultIfEmpty()
                                 orderby mv.Fecha descending, mv.Id descending
                                 select new MovimientoMedicamentoItem
                                 {
                                     Tipo = mv.Tipo,
                                     Cantidad = mv.Cantidad,
                                     CantidadResultante = mv.CantidadResultante,
                                     Fecha = mv.Fecha,
                                     Beneficiario = mv.Beneficiario != null ? mv.Beneficiario.Nombre : null,
                                     Nota = mv.Nota,
                                     RegistradoPor = u != null ? u.NombreCompleto : null
                                 }).Take(200).ToListAsync();

        var beneficiarios = await _db.Beneficiarios.AsNoTracking()
            .Where(b => b.Estado != EstadoBeneficiario.Fallecido && b.Estado != EstadoBeneficiario.Adoptado)
            .OrderBy(b => b.Nombre)
            .Select(b => new BeneficiarioOpcion(b.Id, b.Nombre, b.Estado))
            .ToListAsync();

        return View(new MedicamentoDetalleViewModel { Medicamento = m, Movimientos = movimientos, Beneficiarios = beneficiarios });
    }

    // ---------- Escenario 2: entrada o uso ----------

    [HttpPost]
    public async Task<IActionResult> Movimiento(int id, MovimientoMedicamentoViewModel model)
    {
        var m = await _db.Medicamentos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (m is null) return NotFound();

        if (model.Tipo is not (TipoMovimientoMedicamento.Entrada or TipoMovimientoMedicamento.Uso))
            return Error(id, "Selecciona si es una entrada o un uso.");
        var cantidad = Formatos.LeerCantidad(model.Cantidad);
        if (cantidad is null || cantidad <= 0)
            return Error(id, "Escribe una cantidad mayor que cero, por ejemplo 2 o 1,5.");
        if (cantidad > CantidadMaxima)
            return Error(id, $"La cantidad no puede superar {Formatos.Cantidad(CantidadMaxima)}.");

        var uso = model.Tipo == TipoMovimientoMedicamento.Uso;
        int? beneficiarioId = null;
        if (uso && model.BeneficiarioId is int bId)
        {
            if (!await _db.Beneficiarios.AnyAsync(b => b.Id == bId)) return Error(id, "Selecciona un beneficiario de la lista.");
            beneficiarioId = bId;
        }
        var nota = string.IsNullOrWhiteSpace(model.Nota) ? null : model.Nota.Trim();
        if (nota?.Length > 300) nota = nota[..300];

        // La cantidad y el movimiento se guardan en la misma transacción (ver InventarioMedicamentos)
        decimal? resultante = null;
        var estrategia = _db.Database.CreateExecutionStrategy();
        await estrategia.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            resultante = await _inventario.MoverAsync(m, model.Tipo!.Value, cantidad.Value, beneficiarioId, nota, UsuarioId);
            if (resultante is not null) await tx.CommitAsync();
        });

        var unidad = Textos.UnidadDe(m.Presentacion);
        if (resultante is null)
            return Error(id, uso
                ? $"No hay suficiente {m.NombreComercial}: quedan {Formatos.Cantidad(m.Cantidad)} {unidad}."
                : $"El total no puede superar {Formatos.Cantidad(CantidadMaxima)} {unidad}.");

        TempData["Mensaje"] = $"{(uso ? "Registraste el uso" : "Registraste la entrada")} de {Formatos.Cantidad(cantidad.Value)} {unidad}. " +
                              $"Ahora hay {Formatos.Cantidad(resultante.Value)} {unidad} de {m.NombreComercial}.";
        return RedirectToAction(nameof(Detalle), new { id });
    }

    private IActionResult Error(int id, string mensaje)
    {
        TempData["Error"] = mensaje;
        return RedirectToAction(nameof(Detalle), new { id });
    }

    private decimal? ValidarCantidad(string? texto, string campo, bool obligatoria, bool permiteCero)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            if (obligatoria) ModelState.AddModelError(campo, "Ingresa la cantidad.");
            return null;
        }
        var valor = Formatos.LeerCantidad(texto);
        if (valor is null)
            ModelState.AddModelError(campo, "Escribe solo números, con máximo dos decimales (por ejemplo 20 o 2,5).");
        else if (valor > CantidadMaxima)
            ModelState.AddModelError(campo, $"La cantidad no puede superar {Formatos.Cantidad(CantidadMaxima)}.");
        else if (!permiteCero && valor == 0)
            ModelState.AddModelError(campo, "La cantidad debe ser mayor que cero.");
        return valor;
    }

    private void ValidarFecha(MedicamentoFormViewModel model)
    {
        // Se permite registrar un medicamento ya vencido (para dejar constancia), pero no fechas absurdas
        if (model.FechaVencimiento is DateTime f && (f.Year < 2000 || f > DateTime.Today.AddYears(15)))
            ModelState.AddModelError(nameof(model.FechaVencimiento), "Revisa la fecha de vencimiento.");
    }

    private static void Copiar(MedicamentoFormViewModel model, Medicamento m, decimal? minima)
    {
        m.NombreComercial = model.NombreComercial.Trim();
        m.PrincipioActivo = model.PrincipioActivo.Trim();
        m.FechaVencimiento = model.FechaVencimiento!.Value.Date;
        m.Uso = model.Uso.Trim();
        m.Via = model.Via!.Value;
        m.Dosis = model.Dosis.Trim();
        m.Presentacion = model.Presentacion!.Value;
        m.CantidadMinima = minima;
    }
}
