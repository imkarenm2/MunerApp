using MunerApp.Domain.Common;
using MunerApp.Domain.Enums;

namespace MunerApp.Domain.Entities;

/// <summary>Entrada o uso de un medicamento (HU-037, escenario 2). No se edita ni se borra.</summary>
public class MovimientoMedicamento : IPerteneceAEsal
{
    public int Id { get; set; }
    public int EsalId { get; set; }
    public int MedicamentoId { get; set; }

    public TipoMovimientoMedicamento Tipo { get; set; }

    /// <summary>Cantidad del movimiento, siempre positiva (el tipo dice si suma o resta).</summary>
    public decimal Cantidad { get; set; }

    /// <summary>Cantidad disponible después del movimiento.</summary>
    public decimal CantidadResultante { get; set; }

    /// <summary>Si fue un uso en un beneficiario, cuál (opcional).</summary>
    public int? BeneficiarioId { get; set; }
    public string? Nota { get; set; }

    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public string? RegistradoPorId { get; set; }

    public Medicamento? Medicamento { get; set; }
    public Beneficiario? Beneficiario { get; set; }
}
