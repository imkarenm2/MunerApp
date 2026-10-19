using MunerApp.Domain.Common;

namespace MunerApp.Domain.Entities;

/// <summary>Configuración de las alertas de salud de una fundación (HU-039). Una fila por ESAL.</summary>
public class ConfigSalud : IPerteneceAEsal
{
    public const int DiasMinimos = 1;
    public const int DiasMaximos = 180;

    public int EsalId { get; set; }

    /// <summary>Con cuántos días de anticipación se avisa que un medicamento va a vencer.</summary>
    public int DiasAvisoVencimiento { get; set; } = Medicamento.DiasPorVencerPredeterminado;

    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;
}
