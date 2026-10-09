namespace MunerApp.Domain.Enums;

/// <summary>
/// Estados del beneficiario. Los cuatro últimos son el "fin de reseña" del formato de la fundación:
/// adoptado, liberado, encontró su hogar o falleció.
/// </summary>
public enum EstadoBeneficiario
{
    EnLaFundacion,
    EnTratamiento,
    Adoptable,
    Adoptado,
    Fallecido,
    Liberado,
    EncontroSuHogar
}
