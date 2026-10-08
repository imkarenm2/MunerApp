using MunerApp.Domain.Common;
using MunerApp.Domain.Enums;

namespace MunerApp.Domain.Entities;

/// <summary>
/// Hoja de vida de un beneficiario (en el piloto, un gato). Información interna de la fundación (HU-017).
/// </summary>
public class Beneficiario : IPerteneceAEsal
{
    public int Id { get; set; }
    public int EsalId { get; set; }

    public string Nombre { get; set; } = string.Empty;

    /// <summary>Fecha de nacimiento aproximada: se calcula a partir de la edad que escribe la fundación.</summary>
    public DateTime FechaNacimiento { get; set; }

    public SexoBeneficiario Sexo { get; set; }
    public string Color { get; set; } = string.Empty;
    public DateTime FechaRescate { get; set; }
    public EstadoBeneficiario Estado { get; set; } = EstadoBeneficiario.EnLaFundacion;

    /// <summary>Foto interna (archivo privado: solo la ven los usuarios de la fundación).</summary>
    public string? FotoRuta { get; set; }

    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    public string? RegistradoPorId { get; set; }

    public Esal? Esal { get; set; }
    public AdoptanteBeneficiario? Adoptante { get; set; }
    public ICollection<HistorialEstadoBeneficiario> HistorialEstados { get; set; } = new List<HistorialEstadoBeneficiario>();
}
