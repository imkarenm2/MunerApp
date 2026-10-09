using MunerApp.Domain.Common;

namespace MunerApp.Domain.Entities;

/// <summary>
/// Datos de la persona que adoptó al beneficiario, para el seguimiento posterior (HU-017, escenario 3).
/// Son datos personales: solo los ven los administradores de la fundación.
/// </summary>
public class AdoptanteBeneficiario : IPerteneceAEsal
{
    public int BeneficiarioId { get; set; }
    public int EsalId { get; set; }

    public string Nombre { get; set; } = string.Empty;
    public string? Documento { get; set; }
    public string Telefono { get; set; } = string.Empty;
    public string? Correo { get; set; }
    public string Ciudad { get; set; } = string.Empty;
    public string? Direccion { get; set; }

    /// <summary>Número del formulario de adopción en papel (formato de la fundación).</summary>
    public string? NumeroFormulario { get; set; }

    /// <summary>Quién elaboró el registro de la adopción.</summary>
    public string? Elaboro { get; set; }
    public DateTime FechaAdopcion { get; set; }
    public string? Observaciones { get; set; }

    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    public string? RegistradoPorId { get; set; }

    public Beneficiario? Beneficiario { get; set; }
}
