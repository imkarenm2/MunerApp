using MunerApp.Domain.Common;

namespace MunerApp.Domain.Entities;

/// <summary>
/// Configuración del proceso de adopción de cada fundación (HU-029, HU-032).
/// Si la fundación no la ha configurado, se usan los valores predeterminados del piloto.
/// </summary>
public class ConfigAdopcion : IPerteneceAEsal
{
    public int EsalId { get; set; }

    /// <summary>Recomendaciones y responsabilidades que la persona lee antes del formulario. Una por línea.</summary>
    public string Recomendaciones { get; set; } = string.Empty;

    /// <summary>Aporte que se cubre al adoptar (esterilización y vacuna), en pesos (HU-032).</summary>
    public decimal ValorAporte { get; set; } = ValorAportePredeterminado;

    /// <summary>Mensaje informativo sobre toxoplasmosis que se muestra en la sección de hogar (HU-032, escenario 2).</summary>
    public string? MensajeToxoplasmosis { get; set; }

    /// <summary>Imagen pública que acompaña el mensaje de toxoplasmosis, definida por la fundación.</summary>
    public string? ImagenToxoplasmosisRuta { get; set; }

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

    /// <summary>Valor del piloto: $100.000 para esterilización y vacuna.</summary>
    public const decimal ValorAportePredeterminado = 100_000m;

    /// <summary>
    /// Información general sobre toxoplasmosis (recomendaciones de higiene de los CDC).
    /// Es informativa: cada fundación puede ajustarla y no reemplaza la consulta médica.
    /// </summary>
    public const string MensajeToxoplasmosisPredeterminado =
        "La toxoplasmosis es una infección causada por un parásito. Convivir con un gato no impide un embarazo saludable " +
        "si se siguen medidas sencillas de higiene: idealmente, que otra persona limpie el arenero; si no es posible, " +
        "hacerlo a diario, con guantes y lavándose bien las manos. Mantener al gato dentro de casa y no darle carne cruda. " +
        "Ante cualquier duda, consulta a tu médico.";

    /// <summary>Divide el texto en una recomendación por línea, sin líneas vacías.</summary>
    public static IReadOnlyList<string> ComoLista(string? texto) =>
        (string.IsNullOrWhiteSpace(texto) ? RecomendacionesPredeterminadas : texto)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
}
