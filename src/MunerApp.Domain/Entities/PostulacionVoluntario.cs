using MunerApp.Domain.Common;
using MunerApp.Domain.Enums;

namespace MunerApp.Domain.Entities;

/// <summary>Postulación de una persona como voluntaria en una fundación (HU-035).</summary>
public class PostulacionVoluntario : IPerteneceAEsal
{
    public int Id { get; set; }
    public int EsalId { get; set; }
    public string UsuarioId { get; set; } = string.Empty;
    public TipoPostulacion Tipo { get; set; }
    public string Telefono { get; set; } = string.Empty;
    public string Disponibilidad { get; set; } = string.Empty;
    public string? Motivacion { get; set; }

    // Solo para practicantes de salud
    public string? Institucion { get; set; }
    public string? Programa { get; set; }
    public int? Semestre { get; set; }
    public string? SoporteAcademicoRuta { get; set; }

    public EstadoPostulacion Estado { get; set; } = EstadoPostulacion.Pendiente;
    public DateTime FechaPostulacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaRespuesta { get; set; }

    // ---- Revisión de la fundación (HU-036) ----

    /// <summary>Por qué no se aprobó: lo ve la persona.</summary>
    public string? MotivoRechazo { get; set; }
    public string? RevisadoPorId { get; set; }

    /// <summary>Cuándo la fundación desvinculó al voluntario (escenario 3).</summary>
    public DateTime? FechaRetiro { get; set; }

    public Esal? Esal { get; set; }
}
