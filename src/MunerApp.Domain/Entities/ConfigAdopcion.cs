using MunerApp.Domain.Common;

namespace MunerApp.Domain.Entities;

/// <summary>
/// Configuración del proceso de adopción de cada fundación (HU-029).
/// Si la fundación no la ha configurado, se muestran las recomendaciones predeterminadas.
/// </summary>
public class ConfigAdopcion : IPerteneceAEsal
{
    public int EsalId { get; set; }

    /// <summary>Recomendaciones y responsabilidades que la persona lee antes del formulario. Una por línea.</summary>
    public string Recomendaciones { get; set; } = string.Empty;

    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;

    public Esal? Esal { get; set; }

    /// <summary>Recomendaciones del piloto (El Reino de los Gatos), que cada fundación puede ajustar.</summary>
    public const string RecomendacionesPredeterminadas =
        "Adoptar es un compromiso para toda la vida del animal, que puede superar los 15 años.\n" +
        "Todas las personas del hogar deben estar de acuerdo con la adopción.\n" +
        "Debes cubrir su alimentación, arena, controles veterinarios, vacunas y desparasitaciones.\n" +
        "Al momento de la adopción se cubre el valor de la esterilización y la vacuna, y se lleva un huacal para transportarlo.\n" +
        "La fundación puede hacer una visita de seguimiento a tu hogar después de la adopción.\n" +
        "Firmarás un contrato de adopción y presentarás tu documento de identidad en la cita presencial.\n" +
        "Si en algún momento no puedes seguir cuidándolo, debes informarlo a la fundación: nunca abandonarlo.";

    /// <summary>Divide el texto en una recomendación por línea, sin líneas vacías.</summary>
    public static IReadOnlyList<string> ComoLista(string? texto) =>
        (string.IsNullOrWhiteSpace(texto) ? RecomendacionesPredeterminadas : texto)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
}
