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
    /// <summary>Tope de cantidad por medicamento o movimiento, para evitar errores de digitación.</summary>
    private const decimal CantidadMaxima = 100_000;

    private readonly MunerAppDbContext _db;
    private readonly IEsalActual _esalActual;

    public MedicamentosController(MunerAppDbContext db, IEsalActual esalActual)
    {
        _db = db;
        _esalActual = esalActual;
    }

    private string UsuarioId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private int EsalId => _esalActual.EsalId ?? throw new InvalidOperationException("El usuario no pertenece a una ESAL.");

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

        return View(new MedicamentosIndexViewModel { Busqueda = q, Medicamentos = medicamentos });
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

        // La cantidad se actualiza en la base de datos de forma atómica: dos usos al mismo tiempo
        // nunca dejan el inventario en negativo. El movimiento se guarda en la misma transacción.
        decimal? resultante = null;
        var estrategia = _db.Database.CreateExecutionStrategy();
        await estrategia.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            var filas = uso
                ? await _db.Medicamentos.Where(x => x.Id == id && x.Cantidad >= cantidad)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Cantidad, x => x.Cantidad - cantidad.Value))
                : await _db.Medicamentos.Where(x => x.Id == id && x.Cantidad + cantidad <= CantidadMaxima)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Cantidad, x => x.Cantidad + cantidad.Value));
            if (filas == 0) return;

            resultante = await _db.Medicamentos.Where(x => x.Id == id).Select(x => x.Cantidad).FirstAsync();
            _db.MovimientosMedicamento.Add(new MovimientoMedicamento
            {
                EsalId = m.EsalId,
                MedicamentoId = id,
                Tipo = model.Tipo!.Value,
                Cantidad = cantidad.Value,
                CantidadResultante = resultante.Value,
                BeneficiarioId = beneficiarioId,
                Nota = nota,
                RegistradoPorId = UsuarioId
            });
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
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
