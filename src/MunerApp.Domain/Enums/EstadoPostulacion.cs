namespace MunerApp.Domain.Enums;

/// <summary>
/// Estado de una postulación de voluntario (HU-035). La fundación la aprueba o rechaza (HU-036);
/// Retirada: era voluntario y la fundación lo desvinculó (HU-036, escenario 3).
/// </summary>
public enum EstadoPostulacion
{
    Pendiente,
    Aprobada,
    Rechazada,
    Retirada
}
