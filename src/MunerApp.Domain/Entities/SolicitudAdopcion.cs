using MunerApp.Domain.Common;
using MunerApp.Domain.Enums;

namespace MunerApp.Domain.Entities;

/// <summary>
/// Solicitud de adopción que una persona diligencia en línea (HU-029 a HU-032)
/// y que la fundación gestiona hasta la cita presencial (HU-033, HU-034).
/// Contiene datos personales: solo la ven el solicitante y los administradores de la fundación.
/// </summary>
public class SolicitudAdopcion : IPerteneceAEsal
{
    public int Id { get; set; }
    public int EsalId { get; set; }
    public string UsuarioId { get; set; } = string.Empty;

    public EstadoSolicitudAdopcion Estado { get; set; } = EstadoSolicitudAdopcion.Borrador;

    /// <summary>Momento en que la persona aceptó las recomendaciones y responsabilidades (HU-029).</summary>
    public DateTime FechaAceptacionRecomendaciones { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;

    public Esal? Esal { get; set; }

    /// <summary>Estados en los que la solicitud sigue en trámite: la persona no puede iniciar otra en la misma fundación.</summary>
    public static readonly EstadoSolicitudAdopcion[] EstadosEnProceso = { EstadoSolicitudAdopcion.Recibida };
}
