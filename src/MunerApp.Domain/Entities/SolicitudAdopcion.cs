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

    /// <summary>Secciones del formulario guardadas (0 a 3). Indica dónde continúa la persona si vuelve.</summary>
    public int SeccionesCompletadas { get; set; }

    // ---- Sección 1: datos personales y de contacto (HU-030) ----

    public string? NombreCompleto { get; set; }
    public string? Cedula { get; set; }
    public int? Edad { get; set; }
    public string? Celular { get; set; }
    public string? Ciudad { get; set; }
    public string? Direccion { get; set; }
    public OcupacionAdoptante? Ocupacion { get; set; }

    /// <summary>A qué se dedica, obligatorio si la ocupación es Independiente (HU-030, escenario 2).</summary>
    public string? DetalleOcupacion { get; set; }

    public string? ReferenciaNombre { get; set; }
    public string? ReferenciaCelular { get; set; }

    /// <summary>Relación con la referencia: amigo, familiar, compañero de trabajo...</summary>
    public string? ReferenciaRelacion { get; set; }

    // ---- Sección 2: mascotas (HU-031) ----

    public TenenciaMascotas? Mascotas { get; set; }

    // Si tiene mascotas: qué tipo (puede tener varias)
    public bool TieneGato { get; set; }
    public bool TienePerro { get; set; }
    public bool TieneOtraMascota { get; set; }
    public string? OtraMascota { get; set; }

    // Si tiene gato
    public bool? GatoUsaArenero { get; set; }
    public EsterilizacionMascotas? GatoEsterilizacion { get; set; }
    public VacunasGato? GatoVacunas { get; set; }

    /// <summary>Carné de vacunas (archivo privado, opcional), solo si las vacunas son completas o parciales.</summary>
    public string? CarneVacunasRuta { get; set; }

    // Si tiene perro
    public SociabilidadPerro? PerroSociabilidad { get; set; }

    /// <summary>Si tuvo mascotas: qué ocurrió con ellas (HU-031, escenario 3).</summary>
    public string? QuePasoMascota { get; set; }

    // ---- Sección 3: hogar y compromisos (HU-032) ----

    public TipoVivienda? TipoVivienda { get; set; }
    public TenenciaVivienda? TenenciaVivienda { get; set; }

    /// <summary>Solo si la vivienda es arrendada.</summary>
    public bool? ArrendadorPermiteMascotas { get; set; }

    /// <summary>Personas que viven en el hogar, incluido el solicitante.</summary>
    public int? Convivientes { get; set; }
    public bool? ConvivientesDeAcuerdo { get; set; }

    public bool? NinosEnCasa { get; set; }

    /// <summary>Solo si hay niños en casa (HU-032, escenario 1).</summary>
    public bool? NinosInteractuanMascotas { get; set; }

    /// <summary>¿Alguien en el hogar está embarazada o planea estarlo? Muestra la información de toxoplasmosis (escenario 2).</summary>
    public bool? EmbarazoEnHogar { get; set; }

    public bool? PuedeCubrirCostos { get; set; }
    public bool? AceptaVisita { get; set; }

    // Aceptaciones obligatorias para enviar la solicitud (escenario 3)
    public bool AceptaRequisito { get; set; }
    public bool AceptaContrato { get; set; }
    public bool AutorizaDatos { get; set; }

    /// <summary>Momento de la autorización de tratamiento de datos personales (Ley 1581 de 2012).</summary>
    public DateTime? FechaAutorizacionDatos { get; set; }

    // ---- Envío ----

    /// <summary>Código que ve el solicitante, por ejemplo ADO-2026-000012. Se asigna al enviar.</summary>
    public string? Codigo { get; set; }
    public DateTime? FechaEnvio { get; set; }

    // ---- Revisión de la fundación (HU-033) ----

    /// <summary>Por qué no se aprobó: lo ve el solicitante.</summary>
    public string? MotivoRechazo { get; set; }
    public DateTime? FechaRevision { get; set; }
    public string? RevisadoPorId { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;

    public Esal? Esal { get; set; }

    /// <summary>Estados en los que la solicitud sigue en trámite: la persona no puede iniciar otra en la misma fundación.</summary>
    public static readonly EstadoSolicitudAdopcion[] EstadosEnProceso =
        { EstadoSolicitudAdopcion.Recibida, EstadoSolicitudAdopcion.AprobadaParaCita };
}
