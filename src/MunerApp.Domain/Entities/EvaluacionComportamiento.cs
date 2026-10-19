using MunerApp.Domain.Common;
using MunerApp.Domain.Enums;

namespace MunerApp.Domain.Entities;

/// <summary>
/// Etograma: conductas observadas en su ambiente habitual, antes o después de la castración
/// (formato "Historia clínica e ingreso" de El Reino de los Gatos).
/// </summary>
public class EvaluacionComportamiento : IPerteneceAEsal
{
    public int Id { get; set; }
    public int EsalId { get; set; }
    public int BeneficiarioId { get; set; }
    public MomentoEtograma Momento { get; set; }
    public DateTime Fecha { get; set; }

    /// <summary>Códigos de las conductas observadas, separados por coma (ver Constantes.Etograma).</summary>
    public string Conductas { get; set; } = string.Empty;

    public string Metodo { get; set; } = "Muestreo continuo focal";
    public string? Observaciones { get; set; }
    public string? RegistradoPorId { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public Beneficiario? Beneficiario { get; set; }
}
