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

    /// <summary>Pregunta 6: otro teléfono de contacto (opcional, celular o fijo).</summary>
    public string? TelefonoAdicional { get; set; }
    public string? Ciudad { get; set; }
    public string? Direccion { get; set; }

    /// <summary>Pregunta 9: barrio o conjunto.</summary>
    public string? Barrio { get; set; }
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

    /// <summary>Pregunta 12.1.1: cuántos años tiene(n) su(s) mascota(s), en texto libre (puede tener varias).</summary>
    public string? EdadMascotas { get; set; }

    /// <summary>Pregunta 12.1.3: castrado y vacunas en una sola respuesta, con las 4 opciones del formato.</summary>
    public EstadoSaludGato? GatoSalud { get; set; }

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

    /// <summary>Pregunta 13: por qué quiere adoptar un gatito de compañía.</summary>
    public string? MotivoAdopcion { get; set; }

    // ---- Sección 3: hogar y compromisos (HU-032) ----

    public TipoVivienda? TipoVivienda { get; set; }
    public TenenciaVivienda? TenenciaVivienda { get; set; }

    /// <summary>Solo si la vivienda es arrendada.</summary>
    public bool? ArrendadorPermiteMascotas { get; set; }

    /// <summary>Personas que viven en el hogar, incluido el solicitante.</summary>
    public int? Convivientes { get; set; }
    public bool? ConvivientesDeAcuerdo { get; set; }

    /// <summary>Pregunta 15: si busca algún gato en especial y por qué, o si quiere enamorarse de cualquiera.</summary>
    public string? GatoBuscado { get; set; }

    /// <summary>Pregunta 16: si está dispuesto a cuidarlo unos quince años.</summary>
    public bool? CuidaraQuinceAnios { get; set; }

    /// <summary>Pregunta 17: si lo llevaría a cualquier vivienda, ciudad o país a donde se mude.</summary>
    public bool? LoLlevariaAlMudarse { get; set; }

    /// <summary>Pregunta 18: qué lugar del hogar sería para la mascota.</summary>
    public string? LugarParaMascota { get; set; }

    /// <summary>Pregunta 20: si hay niños y de qué edad. <see cref="NinosEnCasa"/> se calcula a partir de esta respuesta.</summary>
    public NinosEnHogar? NinosEdades { get; set; }

    /// <summary>Pregunta 20, opción "Otro, especifique".</summary>
    public string? NinosOtro { get; set; }

    public bool? NinosEnCasa { get; set; }

    /// <summary>Solo si hay niños en casa (HU-032, escenario 1).</summary>
    public bool? NinosInteractuanMascotas { get; set; }

    /// <summary>¿Alguien en el hogar está embarazada o planea estarlo? Muestra la información de toxoplasmosis (escenario 2).</summary>
    public bool? EmbarazoEnHogar { get; set; }

    /// <summary>Pregunta 22: si conoce sobre la toxoplasmosis.</summary>
    public bool? ConoceToxoplasmosis { get; set; }

    /// <summary>Pregunta 24: para el solicitante, la mejor cualidad de un gato.</summary>
    public string? MejorCualidadGato { get; set; }

    /// <summary>Pregunta 25: cuánto dinero cree que cubre las necesidades de un gato al mes.</summary>
    public CostoMensualGato? CostoMensualEstimado { get; set; }

    public bool? PuedeCubrirCostos { get; set; }
    public bool? AceptaVisita { get; set; }

    // Aceptaciones obligatorias para enviar la solicitud (escenario 3)
    public bool AceptaRequisito { get; set; }
    public bool AceptaContrato { get; set; }
    public bool AutorizaDatos { get; set; }

    /// <summary>Pregunta 30: comentarios y observaciones (opcional).</summary>
    public string? Comentarios { get; set; }

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

    // ---- Cita presencial y resultado (HU-034) ----

    /// <summary>Fecha y hora de la cita, en UTC.</summary>
    public DateTime? FechaCita { get; set; }
    public string? LugarCita { get; set; }

    /// <summary>Indicaciones adicionales de la fundación para la cita (opcional).</summary>
    public string? IndicacionesCita { get; set; }

    /// <summary>Cuántas veces se cambió la fecha de la cita (HU-034, escenario 2).</summary>
    public int Reprogramaciones { get; set; }

    /// <summary>Beneficiario que se llevó la persona, si la adopción se concretó (escenario 3).</summary>
    public int? BeneficiarioAdoptadoId { get; set; }
    public string? ObservacionesResultado { get; set; }
    public DateTime? FechaResultado { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;

    public Esal? Esal { get; set; }
    public Beneficiario? BeneficiarioAdoptado { get; set; }

    /// <summary>Estados en los que la solicitud sigue en trámite: la persona no puede iniciar otra en la misma fundación.</summary>
    public static readonly EstadoSolicitudAdopcion[] EstadosEnProceso =
        { EstadoSolicitudAdopcion.Recibida, EstadoSolicitudAdopcion.AprobadaParaCita, EstadoSolicitudAdopcion.CitaAgendada };
}
