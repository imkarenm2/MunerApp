using System.Security.Claims;
using System.Text;
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
using MunerApp.Web.Filtros;
using MunerApp.Web.Seguridad;
using MunerApp.Web.Servicios;

namespace MunerApp.Web.Areas.Fundacion.Controllers;

/// <summary>
/// HU-038: agenda de salud de los beneficiarios (medicación, vacunas y controles).
/// Escenario 1: el evento programado aparece en la agenda del beneficiario y en la agenda general.
/// Escenario 2: al marcarlo como realizado se registra en la historia clínica (HU-019).
/// Escenario 3: un tratamiento recurrente genera todos sus eventos según la frecuencia y la duración.
/// Solo los administradores y los voluntarios de salud; requiere los módulos de beneficiarios y de salud.
/// </summary>
[Area("Fundacion")]
[Authorize(Policy = Politicas.AccesoClinico)]
[RequiereModulo(CodigosModulo.Beneficiarios)]
[RequiereModulo(CodigosModulo.Salud)]
public class AgendaController : Controller
{
    private const string VolverBeneficiario = "beneficiario";
    private const int MaxEventosPorVista = 300;

    /// <summary>Margen para programar algo "ahora mismo" sin que el formulario lo rechace por unos minutos.</summary>
    private static readonly TimeSpan ToleranciaPasado = TimeSpan.FromMinutes(15);

    private readonly MunerAppDbContext _db;
    private readonly IEsalActual _esalActual;
    private readonly InventarioMedicamentos _inventario;

    public AgendaController(MunerAppDbContext db, IEsalActual esalActual, InventarioMedicamentos inventario)
    {
        _db = db;
        _esalActual = esalActual;
        _inventario = inventario;
    }

    private string UsuarioId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private int EsalId => _esalActual.EsalId ?? throw new InvalidOperationException("El usuario no pertenece a una ESAL.");

    /// <summary>Adoptados y fallecidos ya no tienen agenda (sus eventos pendientes se cancelan al cambiar de estado).</summary>
    public static bool PuedeTenerAgenda(EstadoBeneficiario estado) => EstadosBeneficiario.PuedeApadrinarse(estado);

    // ---------- Escenario 1: agenda general ----------

    [HttpGet]
    public async Task<IActionResult> Index(VistaAgenda vista = VistaAgenda.Proximos)
    {
        var ahora = DateTime.UtcNow;
        var hoy = Formatos.Local(ahora).Date;
        var limiteProximos = Formatos.Utc(hoy.AddDays(AgendaGeneralViewModel.DiasProximos)); // fin del séptimo día

        var pendientes = _db.EventosAgenda.AsNoTracking().Where(e => e.Estado == EstadoEventoAgenda.Pendiente);
        var conteos = await pendientes
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Atrasados = g.Count(e => e.FechaProgramada < ahora),
                Proximos = g.Count(e => e.FechaProgramada >= ahora && e.FechaProgramada < limiteProximos),
                MasAdelante = g.Count(e => e.FechaProgramada >= limiteProximos)
            }).FirstOrDefaultAsync();

        var consulta = vista switch
        {
            VistaAgenda.Atrasados => pendientes.Where(e => e.FechaProgramada < ahora).OrderBy(e => e.FechaProgramada),
            VistaAgenda.MasAdelante => pendientes.Where(e => e.FechaProgramada >= limiteProximos).OrderBy(e => e.FechaProgramada),
            VistaAgenda.Realizados => _db.EventosAgenda.AsNoTracking().Where(e => e.Estado == EstadoEventoAgenda.Realizado)
                .OrderByDescending(e => e.FechaRealizado),
            _ => pendientes.Where(e => e.FechaProgramada >= ahora && e.FechaProgramada < limiteProximos).OrderBy(e => e.FechaProgramada)
        };
        var eventos = await Proyectar(consulta.ThenBy(e => e.Id).Take(MaxEventosPorVista), ahora).ToListAsync();

        return View(new AgendaGeneralViewModel
        {
            Vista = vista,
            Dias = AgruparPorDia(vista == VistaAgenda.Realizados ? eventos : MarcarProximaDosis(eventos), vista == VistaAgenda.Realizados),
            Atrasados = conteos?.Atrasados ?? 0,
            Proximos = conteos?.Proximos ?? 0,
            MasAdelante = conteos?.MasAdelante ?? 0
        });
    }

    // ---------- Escenario 1: agenda del beneficiario ----------

    [HttpGet]
    public async Task<IActionResult> Beneficiario(int id)
    {
        var b = await _db.Beneficiarios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();

        var ahora = DateTime.UtcNow;
        var eventos = _db.EventosAgenda.AsNoTracking().Where(e => e.BeneficiarioId == id);
        var pendientes = await Proyectar(eventos.Where(e => e.Estado == EstadoEventoAgenda.Pendiente)
            .OrderBy(e => e.FechaProgramada).ThenBy(e => e.Id).Take(MaxEventosPorVista), ahora).ToListAsync();
        var historial = await Proyectar(eventos.Where(e => e.Estado != EstadoEventoAgenda.Pendiente)
            .OrderByDescending(e => e.FechaRealizado ?? e.FechaCancelado).ThenByDescending(e => e.Id).Take(100), ahora).ToListAsync();

        return View(new AgendaBeneficiarioViewModel
        {
            BeneficiarioId = b.Id,
            Nombre = b.Nombre,
            Estado = b.Estado,
            FechaNacimiento = b.FechaNacimiento,
            PuedeProgramar = PuedeTenerAgenda(b.Estado),
            Pendientes = AgruparPorDia(MarcarProximaDosis(pendientes), descendente: false),
            Historial = historial
        });
    }

    // ---------- Escenarios 1 y 3: programar un evento o un tratamiento ----------

    [HttpGet]
    public async Task<IActionResult> Programar(int? beneficiarioId)
    {
        // Se sugiere la próxima hora en punto
        var sugerida = Formatos.Local(DateTime.UtcNow);
        sugerida = sugerida.Date.AddHours(sugerida.Hour + 1);
        var model = new ProgramarEventoViewModel
        {
            BeneficiarioId = beneficiarioId,
            Fecha = sugerida.Date,
            Hora = sugerida.TimeOfDay,
            FrecuenciaCada = "8",
            DuracionDias = "5"
        };
        if (beneficiarioId is int id)
        {
            var b = await _db.Beneficiarios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (b is null) return NotFound();
            if (!PuedeTenerAgenda(b.Estado))
            {
                TempData["Error"] = $"{b.Nombre} está \"{Textos.De(b.Estado)}\": ya no se le programan eventos.";
                return RedirectToAction(nameof(Beneficiario), new { id });
            }
        }
        await LlenarListasAsync(model);
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Programar(ProgramarEventoViewModel model)
    {
        Beneficiario? b = null;
        if (model.BeneficiarioId is int bId)
        {
            b = await _db.Beneficiarios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == bId);
            if (b is null)
                ModelState.AddModelError(nameof(model.BeneficiarioId), "Selecciona un beneficiario de la lista.");
            else if (!PuedeTenerAgenda(b.Estado))
                ModelState.AddModelError(nameof(model.BeneficiarioId), $"{b.Nombre} está \"{Textos.De(b.Estado)}\": ya no se le programan eventos.");
        }

        Medicamento? medicamento = null;
        if (model.MedicamentoId is int mId)
        {
            medicamento = await _db.Medicamentos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == mId);
            var hoyLocal = Formatos.Local(DateTime.UtcNow).Date;
            if (medicamento is null)
                ModelState.AddModelError(nameof(model.MedicamentoId), "Selecciona un medicamento de la lista.");
            else if (medicamento.EstaVencido(hoyLocal))
                ModelState.AddModelError(nameof(model.MedicamentoId), $"{medicamento.NombreComercial} venció el {Formatos.Fecha(medicamento.FechaVencimiento)}: no se puede programar.");
        }

        // La fecha y hora se escriben en hora de Colombia y se guardan en UTC
        DateTime? inicio = null;
        if (model.Fecha is DateTime fecha && model.Hora is TimeSpan hora)
        {
            var hoy = Formatos.Local(DateTime.UtcNow).Date;
            if (hora < TimeSpan.Zero || hora >= TimeSpan.FromDays(1))
                ModelState.AddModelError(nameof(model.Hora), "Revisa la hora.");
            else if (fecha.Date < hoy)
                ModelState.AddModelError(nameof(model.Fecha), "La fecha no puede ser anterior a hoy.");
            else if (fecha.Date > hoy.AddYears(2))
                ModelState.AddModelError(nameof(model.Fecha), "Programa eventos de máximo dos años hacia adelante.");
            else if (Formatos.Utc(fecha.Date + hora) < DateTime.UtcNow.Add(-ToleranciaPasado))
                ModelState.AddModelError(nameof(model.Hora), "Esa hora ya pasó. Programa desde la próxima dosis.");
            else
                inicio = Formatos.Utc(fecha.Date + hora);
        }

        // Escenario 3: frecuencia y duración del tratamiento
        var fechas = new List<DateTime>();
        if (model.Recurrente)
        {
            var cada = LeerEntero(model.FrecuenciaCada);
            var duracion = LeerEntero(model.DuracionDias);
            var intervalo = cada is int c ? EventoAgenda.Intervalo(c, model.FrecuenciaUnidad) : null;
            if (string.IsNullOrWhiteSpace(model.FrecuenciaCada))
                ModelState.AddModelError(nameof(model.FrecuenciaCada), "Indica cada cuánto se repite.");
            else if (intervalo is null)
                ModelState.AddModelError(nameof(model.FrecuenciaCada), model.FrecuenciaUnidad == UnidadFrecuencia.Horas
                    ? "La frecuencia debe estar entre 1 y 72 horas." : "La frecuencia debe estar entre 1 y 90 días.");

            if (duracion is not (>= 1 and <= 365))
                ModelState.AddModelError(nameof(model.DuracionDias), "La duración debe estar entre 1 y 365 días.");
            else if (intervalo is TimeSpan i && inicio is DateTime desde)
            {
                fechas = EventoAgenda.FechasDeTratamiento(desde, i, duracion.Value);
                if (fechas.Count < 2)
                    ModelState.AddModelError(nameof(model.DuracionDias), "Con esa frecuencia y duración habría una sola dosis: prográmalo como evento único.");
                else if (fechas.Count > EventoAgenda.MaxDosisPorTratamiento)
                    ModelState.AddModelError(nameof(model.DuracionDias), $"El tratamiento tendría más de {EventoAgenda.MaxDosisPorTratamiento} dosis. Revisa la frecuencia y la duración.");
            }
        }
        else if (inicio is DateTime unico)
        {
            fechas.Add(unico);
        }

        if (!ModelState.IsValid)
        {
            await LlenarListasAsync(model);
            return View(model);
        }

        var serie = model.Recurrente ? Guid.NewGuid() : (Guid?)null;
        var dosis = string.IsNullOrWhiteSpace(model.Dosis) ? null : model.Dosis.Trim();
        for (var n = 0; n < fechas.Count; n++)
        {
            _db.EventosAgenda.Add(new EventoAgenda
            {
                EsalId = b!.EsalId,
                BeneficiarioId = b.Id,
                Tipo = model.Tipo!.Value,
                FechaProgramada = fechas[n],
                Descripcion = model.Descripcion.Trim(),
                MedicamentoId = medicamento?.Id,
                Dosis = dosis,
                SerieId = serie,
                NumeroDosis = n + 1,
                TotalDosis = fechas.Count,
                CreadoPorId = UsuarioId
            });
        }
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = fechas.Count == 1
            ? $"Programaste el evento de {b!.Nombre} para el {Formatos.FechaHora(fechas[0])}"
            : $"Programaste el tratamiento de {b!.Nombre}: {fechas.Count} dosis. La primera es el {Formatos.FechaHora(fechas[0])} y la última el {Formatos.FechaHora(fechas[^1])}";
        return RedirectToAction(nameof(Beneficiario), new { id = b.Id });
    }

    // ---------- Escenario 2: marcar como realizado ----------

    [HttpGet]
    public async Task<IActionResult> Realizar(int id, string? volver)
    {
        var e = await _db.EventosAgenda.AsNoTracking().Include(x => x.Medicamento).FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return NotFound();
        if (e.Estado != EstadoEventoAgenda.Pendiente)
            return Volver(volver, e.BeneficiarioId, error: "Este evento ya no está pendiente.");

        var model = new RealizarEventoViewModel
        {
            Id = id,
            Volver = volver,
            Fecha = Formatos.Local(DateTime.UtcNow).Date,
            Responsable = User.FindFirstValue(MunerAppClaims.NombreCompleto) ?? ""
        };
        await LlenarRealizarAsync(model);
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Realizar(int id, RealizarEventoViewModel model)
    {
        var e = await _db.EventosAgenda.AsNoTracking().Include(x => x.Medicamento).Include(x => x.Beneficiario)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return NotFound();
        model.Id = id;
        if (e.Estado != EstadoEventoAgenda.Pendiente)
            return Volver(model.Volver, e.BeneficiarioId, error: "Este evento ya no está pendiente.");

        var hoy = Formatos.Local(DateTime.UtcNow).Date;
        if (model.Fecha is DateTime f && f.Date > hoy)
            ModelState.AddModelError(nameof(model.Fecha), "La fecha no puede ser futura.");
        else if (model.Fecha is DateTime f2 && f2.Date < Formatos.Local(e.FechaCreacion).Date.AddDays(-1))
            ModelState.AddModelError(nameof(model.Fecha), "La fecha no puede ser anterior a cuando se programó el evento.");

        decimal? descontar = null;
        if (!string.IsNullOrWhiteSpace(model.CantidadDescontar))
        {
            descontar = Formatos.LeerCantidad(model.CantidadDescontar);
            if (e.Medicamento is null)
                ModelState.AddModelError(nameof(model.CantidadDescontar), "Este evento no tiene un medicamento del inventario.");
            else if (descontar is null || descontar <= 0)
                ModelState.AddModelError(nameof(model.CantidadDescontar), "Escribe una cantidad mayor que cero, por ejemplo 1 o 0,5, o déjalo vacío.");
            else if (descontar > InventarioMedicamentos.CantidadMaxima)
                ModelState.AddModelError(nameof(model.CantidadDescontar), "Revisa la cantidad.");
        }

        if (!ModelState.IsValid)
        {
            await LlenarRealizarAsync(model);
            return View(model);
        }

        var ahora = DateTime.UtcNow;
        var nota = string.IsNullOrWhiteSpace(model.Nota) ? null : model.Nota.Trim();
        var resultado = ResultadoRealizar.Realizado;
        decimal? resultante = null;

        var estrategia = _db.Database.CreateExecutionStrategy();
        await estrategia.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            resultado = ResultadoRealizar.Realizado;
            await using var tx = await _db.Database.BeginTransactionAsync();

            // Solo un usuario puede marcarlo: si dos lo hacen a la vez, el segundo no encuentra el evento pendiente
            var tomado = await _db.EventosAgenda
                .Where(x => x.Id == id && x.Estado == EstadoEventoAgenda.Pendiente)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Estado, EstadoEventoAgenda.Realizado)
                    .SetProperty(x => x.FechaRealizado, ahora)
                    .SetProperty(x => x.RealizadoPorId, UsuarioId));
            if (tomado == 0)
            {
                resultado = ResultadoRealizar.YaNoPendiente;
                return;
            }

            if (descontar is decimal cantidad)
            {
                resultante = await _inventario.MoverAsync(e.Medicamento!, TipoMovimientoMedicamento.Uso, cantidad,
                    e.BeneficiarioId, $"Agenda de salud: {Recortar(e.Descripcion, 200)}", UsuarioId);
                if (resultante is null)
                {
                    resultado = ResultadoRealizar.SinInventario;
                    return; // la transacción se revierte: el evento sigue pendiente
                }
            }

            var clinico = new EventoClinico
            {
                EsalId = e.EsalId,
                BeneficiarioId = e.BeneficiarioId,
                Tipo = e.Tipo,
                Fecha = model.Fecha!.Value.Date,
                Descripcion = DescripcionClinica(e, descontar, nota),
                Responsable = model.Responsable.Trim(),
                RegistradoPorId = UsuarioId
            };
            _db.EventosClinicos.Add(clinico);
            await _db.SaveChangesAsync();
            await _db.EventosAgenda.Where(x => x.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.EventoClinicoId, clinico.Id));
            await tx.CommitAsync();
        });

        switch (resultado)
        {
            case ResultadoRealizar.YaNoPendiente:
                return Volver(model.Volver, e.BeneficiarioId, error: "Otra persona ya marcó o canceló este evento.");
            case ResultadoRealizar.SinInventario:
                var actual = await _db.Medicamentos.AsNoTracking().Where(m => m.Id == e.MedicamentoId).Select(m => m.Cantidad).FirstAsync();
                ModelState.AddModelError(nameof(model.CantidadDescontar),
                    $"No hay suficiente {e.Medicamento!.NombreComercial}: quedan {Formatos.Cantidad(actual)} {Textos.UnidadDe(e.Medicamento.Presentacion)}.");
                await LlenarRealizarAsync(model);
                return View(model);
        }

        var mensaje = $"Marcaste como realizado el evento de {e.Beneficiario!.Nombre} y quedó en su historia clínica.";
        if (descontar is decimal d && resultante is decimal r)
        {
            var unidad = Textos.UnidadDe(e.Medicamento!.Presentacion);
            mensaje += $" Se descontaron {Formatos.Cantidad(d)} {unidad} de {e.Medicamento.NombreComercial}; quedan {Formatos.Cantidad(r)} {unidad}.";
        }
        TempData["Mensaje"] = mensaje;
        return Volver(model.Volver, e.BeneficiarioId);
    }

    private enum ResultadoRealizar { Realizado, YaNoPendiente, SinInventario }

    // ---------- Cancelar un evento o lo que queda de un tratamiento ----------

    [HttpPost]
    public async Task<IActionResult> Cancelar(int id, bool serie, string? motivo, string? volver)
    {
        var e = await _db.EventosAgenda.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return NotFound();

        motivo = string.IsNullOrWhiteSpace(motivo) ? null : Recortar(motivo.Trim(), 300);
        var ahora = DateTime.UtcNow;
        var consulta = serie && e.SerieId is Guid s
            // Escenario 3: se cancela esta dosis y las siguientes del tratamiento; las anteriores no se tocan
            ? _db.EventosAgenda.Where(x => x.SerieId == s && x.FechaProgramada >= e.FechaProgramada)
            : _db.EventosAgenda.Where(x => x.Id == id);
        var canceladas = await consulta
            .Where(x => x.Estado == EstadoEventoAgenda.Pendiente)
            .ExecuteUpdateAsync(u => u
                .SetProperty(x => x.Estado, EstadoEventoAgenda.Cancelado)
                .SetProperty(x => x.FechaCancelado, ahora)
                .SetProperty(x => x.CanceladoPorId, UsuarioId)
                .SetProperty(x => x.MotivoCancelacion, motivo));

        if (canceladas == 0)
            return Volver(volver, e.BeneficiarioId, error: "Este evento ya no está pendiente.");

        TempData["Mensaje"] = canceladas == 1 ? "Cancelaste el evento." : $"Cancelaste {canceladas} dosis pendientes del tratamiento.";
        return Volver(volver, e.BeneficiarioId);
    }

    // ---------- Apoyo ----------

    private static IQueryable<EventoAgendaItem> Proyectar(IQueryable<EventoAgenda> consulta, DateTime ahora) =>
        consulta.Select(e => new EventoAgendaItem
        {
            Id = e.Id,
            BeneficiarioId = e.BeneficiarioId,
            Beneficiario = e.Beneficiario!.Nombre,
            Tipo = e.Tipo,
            FechaProgramada = e.FechaProgramada,
            Descripcion = e.Descripcion,
            Medicamento = e.Medicamento != null ? e.Medicamento.NombreComercial : null,
            Dosis = e.Dosis,
            SerieId = e.SerieId,
            NumeroDosis = e.NumeroDosis,
            TotalDosis = e.TotalDosis,
            Estado = e.Estado,
            FechaRealizado = e.FechaRealizado ?? e.FechaCancelado,
            MotivoCancelacion = e.MotivoCancelacion,
            Atrasado = e.Estado == EstadoEventoAgenda.Pendiente && e.FechaProgramada < ahora
        });

    /// <summary>
    /// "Suspender tratamiento" se ofrece solo en la próxima dosis pendiente de cada tratamiento (la lista viene
    /// ordenada por fecha), para no repetir el botón en todas las dosis.
    /// </summary>
    private static List<EventoAgendaItem> MarcarProximaDosis(List<EventoAgendaItem> pendientes)
    {
        var vistas = new HashSet<Guid>();
        foreach (var e in pendientes)
            e.EsProximaDosis = e.SerieId is Guid s && vistas.Add(s);
        return pendientes;
    }

    /// <summary>Agrupa por día en hora de Colombia: por fecha programada, o por fecha de realización en el historial.</summary>
    private static List<DiaAgenda> AgruparPorDia(List<EventoAgendaItem> eventos, bool descendente) =>
        eventos
            .GroupBy(e => Formatos.Local(descendente ? e.FechaRealizado ?? e.FechaProgramada : e.FechaProgramada).Date)
            .Select(g => new DiaAgenda(g.Key, g.ToList()))
            .ToList();

    private async Task LlenarListasAsync(ProgramarEventoViewModel model)
    {
        model.Beneficiarios = await _db.Beneficiarios.AsNoTracking()
            .Where(b => b.Estado != EstadoBeneficiario.Adoptado && b.Estado != EstadoBeneficiario.Fallecido)
            .OrderBy(b => b.Nombre)
            .Select(b => new BeneficiarioOpcion(b.Id, b.Nombre, b.Estado))
            .ToListAsync();
        model.NombreBeneficiario = model.BeneficiarioId is int id ? model.Beneficiarios.FirstOrDefault(b => b.Id == id)?.Nombre : null;

        // Solo medicamentos vigentes: no se programa la aplicación de uno vencido
        var hoy = Formatos.Local(DateTime.UtcNow).Date;
        model.Medicamentos = await _db.Medicamentos.AsNoTracking()
            .Where(m => m.FechaVencimiento >= hoy)
            .OrderBy(m => m.NombreComercial)
            .Select(m => new MedicamentoOpcion(m.Id, m.NombreComercial, m.Dosis, m.Presentacion))
            .ToListAsync();
    }

    private async Task LlenarRealizarAsync(RealizarEventoViewModel model)
    {
        model.Evento = await Proyectar(_db.EventosAgenda.AsNoTracking().Where(x => x.Id == model.Id), DateTime.UtcNow).FirstAsync();
        var m = await _db.EventosAgenda.AsNoTracking().Where(x => x.Id == model.Id && x.Medicamento != null)
            .Select(x => new { x.Medicamento!.Cantidad, x.Medicamento.Presentacion }).FirstOrDefaultAsync();
        model.Disponible = m?.Cantidad;
        model.Presentacion = m?.Presentacion;
    }

    /// <summary>Texto que queda en la historia clínica: lo programado, la dosis del tratamiento y lo que se observó.</summary>
    private static string DescripcionClinica(EventoAgenda e, decimal? descontado, string? nota)
    {
        var sb = new StringBuilder(e.Descripcion);
        if (e.Medicamento is not null)
            sb.Append($"\nMedicamento: {e.Medicamento.NombreComercial} ({e.Medicamento.PrincipioActivo}).");
        if (e.Dosis is not null)
            sb.Append($"\nDosis: {e.Dosis}.");
        if (e.SerieId is not null)
            sb.Append($"\nDosis {e.NumeroDosis} de {e.TotalDosis} del tratamiento.");
        if (descontado is decimal d && e.Medicamento is not null)
            sb.Append($"\nSe descontaron {Formatos.Cantidad(d)} {Textos.UnidadDe(e.Medicamento.Presentacion)} del inventario.");
        sb.Append($"\nProgramado en la agenda de salud para el {Formatos.FechaHora(e.FechaProgramada)}");
        if (nota is not null)
            sb.Append($"\nObservaciones: {nota}");
        return Recortar(sb.ToString(), 1000);
    }

    private static int? LeerEntero(string? texto) =>
        int.TryParse(texto?.Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : null;

    private static string Recortar(string texto, int max) => texto.Length <= max ? texto : texto[..(max - 1)] + "…";

    private IActionResult Volver(string? volver, int beneficiarioId, string? error = null)
    {
        if (error is not null) TempData["Error"] = error;
        return volver == VolverBeneficiario
            ? RedirectToAction(nameof(Beneficiario), new { id = beneficiarioId })
            : RedirectToAction(nameof(Index));
    }
}
