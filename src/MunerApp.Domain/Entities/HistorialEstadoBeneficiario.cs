using MunerApp.Domain.Common;
using MunerApp.Domain.Enums;

namespace MunerApp.Domain.Entities;

/// <summary>Cada cambio de estado de un beneficiario, con su fecha (HU-017, escenario 2).</summary>
public class HistorialEstadoBeneficiario : IPerteneceAEsal
{
    public int Id { get; set; }
    public int EsalId { get; set; }
    public int BeneficiarioId { get; set; }
    public EstadoBeneficiario Estado { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public string? CambiadoPorId { get; set; }
    public string? Nota { get; set; }

    public Beneficiario? Beneficiario { get; set; }
}
