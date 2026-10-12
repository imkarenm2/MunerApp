using MunerApp.Domain.Common;
using MunerApp.Domain.Enums;

namespace MunerApp.Domain.Entities;

/// <summary>
/// Evento de la historia clínica de un beneficiario: vacuna, tratamiento, control u otro (HU-019).
/// Es información interna; no se edita ni se borra para conservar el historial.
/// </summary>
public class EventoClinico : IPerteneceAEsal
{
    public int Id { get; set; }
    public int EsalId { get; set; }
    public int BeneficiarioId { get; set; }

    public TipoEventoClinico Tipo { get; set; }

    /// <summary>Día en que ocurrió el evento.</summary>
    public DateTime Fecha { get; set; }

    public string Descripcion { get; set; } = string.Empty;

    /// <summary>Vacuna aplicada, desparasitante o prueba (p. ej. "Triple felina", "Galgocal 200", "VIF").</summary>
    public string? Producto { get; set; }

    /// <summary>Laboratorio de la vacuna (p. ej. Feligen, Virbac).</summary>
    public string? Laboratorio { get; set; }

    /// <summary>Peso del día (desparasitación y controles).</summary>
    public decimal? PesoKg { get; set; }

    /// <summary>Resultado de una prueba (p. ej. VIF negativo).</summary>
    public string? Resultado { get; set; }

    /// <summary>Quien atendió o aplicó (puede ser un veterinario externo).</summary>
    public string Responsable { get; set; } = string.Empty;

    /// <summary>Usuario de la plataforma que lo registró.</summary>
    public string? RegistradoPorId { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public Beneficiario? Beneficiario { get; set; }
    public ICollection<FotoEventoClinico> Fotos { get; set; } = new List<FotoEventoClinico>();
}
