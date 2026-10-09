namespace MunerApp.Domain.Enums;

/// <summary>
/// Cómo llegó la donación: reportada a mano con soporte (HU-014) o pagada en línea con Wompi (HU-043),
/// que se confirma sola con el aviso de Wompi (HU-044).
/// </summary>
public enum OrigenDonacion
{
    Manual,
    Wompi
}
