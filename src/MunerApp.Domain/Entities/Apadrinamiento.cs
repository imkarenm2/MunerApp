using MunerApp.Domain.Common;
using MunerApp.Domain.Enums;

namespace MunerApp.Domain.Entities;

/// <summary>
/// Un donante (padrino) que apoya a un beneficiario con un aporte mensual (HU-021).
/// El aporte no se cobra automáticamente: el padrino transfiere cada mes y reporta su aporte,
/// que sigue el mismo flujo de confirmación de las donaciones (<see cref="Donacion.ApadrinamientoId"/>).
/// </summary>
public class Apadrinamiento : IPerteneceAEsal
{
    public int Id { get; set; }
    public int EsalId { get; set; }
    public int BeneficiarioId { get; set; }
    public string PadrinoId { get; set; } = string.Empty;

    /// <summary>Aporte mensual que el padrino se compromete a hacer, en pesos.</summary>
    public decimal ValorMensual { get; set; }

    public EstadoApadrinamiento Estado { get; set; } = EstadoApadrinamiento.Activo;
    public DateTime FechaInicio { get; set; } = DateTime.UtcNow;
    public DateTime? FechaCancelacion { get; set; }

    public Esal? Esal { get; set; }
    public Beneficiario? Beneficiario { get; set; }
}
