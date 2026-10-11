using MunerApp.Domain.Common;
using MunerApp.Domain.Enums;

namespace MunerApp.Domain.Entities;

/// <summary>
/// Medicamento o insumo del inventario clínico de la fundación (HU-037).
/// La cantidad solo cambia con movimientos (<see cref="MovimientoMedicamento"/>), que quedan en el historial.
/// </summary>
public class Medicamento : IPerteneceAEsal
{
    public int Id { get; set; }
    public int EsalId { get; set; }

    public string NombreComercial { get; set; } = string.Empty;
    public string PrincipioActivo { get; set; } = string.Empty;
    public DateTime FechaVencimiento { get; set; }

    /// <summary>Para qué se usa (por ejemplo, "Antiparasitario interno").</summary>
    public string Uso { get; set; } = string.Empty;
    public ViaAdministracion Via { get; set; }

    /// <summary>Dosis habitual, como texto libre (por ejemplo, "1 tableta por cada 4 kg").</summary>
    public string Dosis { get; set; } = string.Empty;

    public PresentacionMedicamento Presentacion { get; set; }
    public decimal Cantidad { get; set; }

    /// <summary>Por debajo de esta cantidad se genera la alerta de faltante (HU-039). Opcional.</summary>
    public decimal? CantidadMinima { get; set; }

    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    public string? RegistradoPorId { get; set; }

    public Esal? Esal { get; set; }
    public ICollection<MovimientoMedicamento> Movimientos { get; set; } = new List<MovimientoMedicamento>();

    /// <summary>Vencido: su fecha de vencimiento ya pasó.</summary>
    public bool EstaVencido(DateTime hoy) => FechaVencimiento.Date < hoy.Date;

    /// <summary>Stock bajo: tiene cantidad mínima y la cantidad actual es menor o igual.</summary>
    public bool TieneStockBajo => CantidadMinima is decimal minima && Cantidad <= minima;

    /// <summary>Días antes del vencimiento en los que un medicamento se considera "por vencer" (HU-039, HU-040).</summary>
    public const int DiasPorVencerPredeterminado = 30;

    /// <summary>Por vencer: aún no vence, pero vence dentro de los próximos <paramref name="dias"/> días.</summary>
    public bool EstaPorVencer(DateTime hoy, int dias = DiasPorVencerPredeterminado)
        => !EstaVencido(hoy) && FechaVencimiento.Date <= hoy.Date.AddDays(dias);
}
