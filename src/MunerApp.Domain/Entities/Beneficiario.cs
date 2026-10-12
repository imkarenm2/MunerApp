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

    /// <summary>true si se conoce la fecha exacta; false si se calculó a partir de la edad aproximada.</summary>
    public bool FechaNacimientoExacta { get; set; }

    public SexoBeneficiario Sexo { get; set; }
    public string Color { get; set; } = string.Empty;

    /// <summary>Fecha de ingreso a la fundación.</summary>
    public DateTime FechaRescate { get; set; }

    // ---- Reseña del paciente (formato "Historia clínica e ingreso" de la fundación) ----

    public string Especie { get; set; } = "Felino";
    public string? Raza { get; set; }
    public decimal? PesoIngresoKg { get; set; }
    public Procedencia? Procedencia { get; set; }
    public string? DetallesProcedencia { get; set; }
    public EstadoReproductivo? EstadoReproductivo { get; set; }
    public string? SenalesParticulares { get; set; }

    // ---- Examen semiológico de ingreso (información clínica) ----

    /// <summary>Frecuencia respiratoria (respiraciones por minuto).</summary>
    public int? FrecuenciaRespiratoria { get; set; }

    /// <summary>Frecuencia cardiaca (latidos por minuto).</summary>
    public int? FrecuenciaCardiaca { get; set; }

    /// <summary>Tiempo de llenado capilar, en segundos.</summary>
    public decimal? Tllc { get; set; }

    /// <summary>Reflejo pupilar a la luz (RPC), en segundos.</summary>
    public decimal? Rpc { get; set; }

    /// <summary>Temperatura en °C.</summary>
    public decimal? Temperatura { get; set; }

    /// <summary>Condición corporal de 1 a 5.</summary>
    public int? CondicionCorporal { get; set; }

    public string? MucosaConjuntival { get; set; }
    public string? MucosaOral { get; set; }
    public string? MucosaRectal { get; set; }
    public string? MucosaVulvarPrepucial { get; set; }
    public EstadoConciencia? EstadoConciencia { get; set; }
    public string? ObservacionesIngreso { get; set; }
    public DateTime? FechaExamenIngreso { get; set; }
    public string? ExamenIngresoPorId { get; set; }
    public EstadoBeneficiario Estado { get; set; } = EstadoBeneficiario.EnLaFundacion;

    /// <summary>Foto interna (archivo privado: solo la ven los usuarios de la fundación).</summary>
    public string? FotoRuta { get; set; }

    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    public string? RegistradoPorId { get; set; }

    // ---- Apadrinamiento (HU-020): lo único que se muestra al público ----

    /// <summary>Si es true aparece en la opción "Apadrinar" del perfil de la fundación.</summary>
    public bool Apadrinable { get; set; }

    /// <summary>Historia corta para el público. Nunca se mezcla con datos internos ni clínicos.</summary>
    public string? HistoriaPublica { get; set; }

    /// <summary>Valor mensual sugerido, en pesos.</summary>
    public decimal? AporteSugerido { get; set; }

    /// <summary>Foto pública (archivo público, distinta de la foto interna de la hoja de vida).</summary>
    public string? FotoPublicaRuta { get; set; }
    public DateTime? FechaApadrinable { get; set; }

    public Esal? Esal { get; set; }
    public AdoptanteBeneficiario? Adoptante { get; set; }
    public ICollection<HistorialEstadoBeneficiario> HistorialEstados { get; set; } = new List<HistorialEstadoBeneficiario>();
}
