namespace MunerApp.Domain.Enums;

/// <summary>
/// Vacunas de los gatos del solicitante (HU-031), en el orden del formulario de la fundación:
/// a) todas, b) ninguna, c) algunas. Con a o c se puede adjuntar el carné.
/// </summary>
public enum VacunasGato
{
    Completas,
    Ninguna,
    Parciales
}
