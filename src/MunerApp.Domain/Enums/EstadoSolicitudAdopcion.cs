namespace MunerApp.Domain.Enums;

/// <summary>
/// Estados de una solicitud de adopción (HU-029 a HU-034).
/// Borrador: la persona aceptó las recomendaciones y está diligenciando el formulario.
/// Recibida: la envió (HU-032). AprobadaParaCita o Rechazada: la fundación la revisó (HU-033).
/// CitaAgendada, AdopcionConcretada o NoConcretada: la cita presencial y su resultado (HU-034).
/// </summary>
public enum EstadoSolicitudAdopcion
{
    Borrador,
    Recibida,
    AprobadaParaCita,
    Rechazada,
    CitaAgendada,
    AdopcionConcretada,
    NoConcretada
}
