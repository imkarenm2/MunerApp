using Microsoft.EntityFrameworkCore;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;

namespace MunerApp.Web.Servicios;

/// <summary>
/// Entradas y usos del inventario de medicamentos (HU-037). Lo usan el inventario y la agenda de salud
/// (HU-038, al descontar la dosis aplicada), para que la regla esté en un solo lugar.
/// </summary>
public class InventarioMedicamentos
{
    /// <summary>Tope de cantidad por medicamento o movimiento, para evitar errores de digitación.</summary>
    public const decimal CantidadMaxima = 100_000;

    private readonly MunerAppDbContext _db;

    public InventarioMedicamentos(MunerAppDbContext db) => _db = db;

    /// <summary>
    /// Suma (entrada) o resta (uso) de forma atómica en la base de datos: dos usos al mismo tiempo nunca dejan
    /// el inventario en negativo. Guarda el movimiento con SaveChanges; debe llamarse dentro de una transacción.
    /// Devuelve la cantidad resultante, o null si no hay suficiente (uso) o se superaría el tope (entrada).
    /// </summary>
    public async Task<decimal?> MoverAsync(Medicamento m, TipoMovimientoMedicamento tipo, decimal cantidad,
        int? beneficiarioId, string? nota, string usuarioId)
    {
        if (tipo is not (TipoMovimientoMedicamento.Entrada or TipoMovimientoMedicamento.Uso))
            throw new ArgumentOutOfRangeException(nameof(tipo));

        var filas = tipo == TipoMovimientoMedicamento.Uso
            ? await _db.Medicamentos.Where(x => x.Id == m.Id && x.Cantidad >= cantidad)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Cantidad, x => x.Cantidad - cantidad))
            : await _db.Medicamentos.Where(x => x.Id == m.Id && x.Cantidad + cantidad <= CantidadMaxima)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Cantidad, x => x.Cantidad + cantidad));
        if (filas == 0) return null;

        var resultante = await _db.Medicamentos.Where(x => x.Id == m.Id).Select(x => x.Cantidad).FirstAsync();
        _db.MovimientosMedicamento.Add(new MovimientoMedicamento
        {
            EsalId = m.EsalId,
            MedicamentoId = m.Id,
            Tipo = tipo,
            Cantidad = cantidad,
            CantidadResultante = resultante,
            BeneficiarioId = beneficiarioId,
            Nota = nota,
            RegistradoPorId = usuarioId
        });
        await _db.SaveChangesAsync();
        return resultante;
    }
}
