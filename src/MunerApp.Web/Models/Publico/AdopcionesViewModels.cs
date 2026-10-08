using System.ComponentModel.DataAnnotations;
using MunerApp.Domain.Enums;
using MunerApp.Web.Validacion;

namespace MunerApp.Web.Models.Publico;

/// <summary>HU-029: recomendaciones y responsabilidades antes del formulario de adopción.</summary>
public class RecomendacionesAdopcionViewModel
{
    // Datos para mostrar
    public string Slug { get; set; } = string.Empty;
    public string NombreEsal { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public IReadOnlyList<string> Recomendaciones { get; set; } = Array.Empty<string>();

    /// <summary>Si ya tiene una solicitud en trámite o es una cuenta de fundación, se muestra el aviso en vez de la aceptación.</summary>
    public string? Aviso { get; set; }

    /// <summary>Si ya aceptó antes y tiene un borrador, el botón dice "Continuar mi solicitud".</summary>
    public bool TieneBorrador { get; set; }

    /// <summary>Escenario 1: debe marcar que acepta para habilitar el formulario.</summary>
    public bool Acepto { get; set; }
}

/// <summary>Datos comunes de cada sección del formulario de adopción (HU-030 a HU-032).</summary>
public class SeccionAdopcionViewModel
{
    public const int TotalSecciones = 3;

    public string Slug { get; set; } = string.Empty;
    public string NombreEsal { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }

    /// <summary>Sección que se está mostrando (1 a 3).</summary>
    public int Seccion { get; set; }

    /// <summary>Secciones ya guardadas: se puede volver a ellas desde los pasos.</summary>
    public int SeccionesCompletadas { get; set; }
}

/// <summary>Sección 1: datos personales y de contacto (HU-030).</summary>
public class DatosPersonalesAdopcionViewModel : SeccionAdopcionViewModel
{
    /// <summary>Correo de la cuenta: la fundación lo usa para contactar a la persona.</summary>
    public string Correo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa tu nombre completo.")]
    [StringLength(150, MinimumLength = 5, ErrorMessage = "Escribe tu nombre y apellidos.")]
    [Display(Name = "Nombre completo")]
    public string NombreCompleto { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa tu número de cédula.")]
    [RegularExpression(ValidadorPersonas.PatronCedula, ErrorMessage = ValidadorPersonas.ErrorCedula)]
    [StringLength(14, ErrorMessage = ValidadorPersonas.ErrorCedula)]
    [Display(Name = "Cédula de ciudadanía")]
    public string Cedula { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa tu edad.")]
    [Range(18, 100, ErrorMessage = "Para adoptar debes ser mayor de edad (18 años o más).")]
    [Display(Name = "Edad")]
    public int? Edad { get; set; }

    [Required(ErrorMessage = "Ingresa tu número de celular.")]
    [RegularExpression(ValidadorPersonas.PatronCelular, ErrorMessage = ValidadorPersonas.ErrorCelular)]
    [Display(Name = "Celular")]
    public string Celular { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa la ciudad o municipio donde vives.")]
    [StringLength(100)]
    [Display(Name = "Ciudad o municipio")]
    public string Ciudad { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa tu dirección.")]
    [StringLength(200, MinimumLength = 5, ErrorMessage = "Escribe la dirección completa, con barrio si aplica.")]
    [Display(Name = "Dirección")]
    public string Direccion { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona tu ocupación.")]
    [Display(Name = "Ocupación")]
    public OcupacionAdoptante? Ocupacion { get; set; }

    /// <summary>Obligatorio si la ocupación es Independiente (escenario 2): se valida en el servidor.</summary>
    [StringLength(150, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "¿A qué te dedicas?")]
    public string? DetalleOcupacion { get; set; }

    [Required(ErrorMessage = "Ingresa el nombre de tu referencia personal.")]
    [StringLength(150, MinimumLength = 5, ErrorMessage = "Escribe el nombre y apellido de tu referencia.")]
    [Display(Name = "Nombre de la referencia")]
    public string ReferenciaNombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa el celular de tu referencia.")]
    [RegularExpression(ValidadorPersonas.PatronCelular, ErrorMessage = ValidadorPersonas.ErrorCelular)]
    [Display(Name = "Celular de la referencia")]
    public string ReferenciaCelular { get; set; } = string.Empty;

    [Required(ErrorMessage = "Indica qué relación tienes con tu referencia.")]
    [StringLength(60)]
    [Display(Name = "Relación")]
    public string ReferenciaRelacion { get; set; } = string.Empty;

    public static readonly string[] Relaciones = { "Familiar", "Amigo(a)", "Compañero(a) de trabajo", "Vecino(a)", "Otra" };
}

/// <summary>
/// Sección 2: mascotas, con preguntas condicionales (HU-031).
/// Las preguntas que dependen de otra respuesta se validan en el controlador.
/// </summary>
public class MascotasAdopcionViewModel : SeccionAdopcionViewModel
{
    [Required(ErrorMessage = "Cuéntanos si tienes o has tenido mascotas.")]
    [Display(Name = "¿Tienes o has tenido mascotas?")]
    public TenenciaMascotas? Mascotas { get; set; }

    // Escenario 1: tipo de mascota y preguntas según el tipo
    public bool TieneGato { get; set; }
    public bool TienePerro { get; set; }
    public bool TieneOtraMascota { get; set; }

    [StringLength(100, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "¿Qué otra mascota?")]
    public string? OtraMascota { get; set; }

    [Display(Name = "¿Tu gato usa arenero?")]
    public bool? GatoUsaArenero { get; set; }

    [Display(Name = "¿Están esterilizados?")]
    public EsterilizacionMascotas? GatoEsterilizacion { get; set; }

    [Display(Name = "¿Están vacunados?")]
    public VacunasGato? GatoVacunas { get; set; }

    /// <summary>Escenario 2: carné de vacunas opcional si las vacunas son completas o parciales.</summary>
    [Display(Name = "Carné de vacunas (opcional)")]
    public IFormFile? CarneVacunas { get; set; }

    /// <summary>Ya adjuntó un carné antes: se conserva si no sube otro.</summary>
    public bool TieneCarne { get; set; }

    public bool QuitarCarne { get; set; }

    [Display(Name = "¿Tu perro es sociable con los gatos?")]
    public SociabilidadPerro? PerroSociabilidad { get; set; }

    /// <summary>Escenario 3: si tuvo mascotas, qué ocurrió con ellas.</summary>
    [StringLength(500, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "¿Qué ocurrió con tu mascota?")]
    public string? QuePasoMascota { get; set; }

    /// <summary>Con las opciones a) o c) de vacunas se puede adjuntar el carné.</summary>
    public static bool PermiteCarne(VacunasGato? vacunas) => vacunas is VacunasGato.Completas or VacunasGato.Parciales;
}

/// <summary>
/// Sección 3: hogar y compromisos, y envío de la solicitud (HU-032).
/// Las preguntas que dependen de otra respuesta se validan en el controlador.
/// </summary>
public class HogarAdopcionViewModel : SeccionAdopcionViewModel
{
    // Datos de la fundación para mostrar
    public decimal ValorAporte { get; set; }
    public string MensajeToxoplasmosis { get; set; } = string.Empty;
    public string? ImagenToxoplasmosisUrl { get; set; }

    [Required(ErrorMessage = "Selecciona el tipo de vivienda.")]
    [Display(Name = "¿En qué tipo de vivienda vives?")]
    public TipoVivienda? TipoVivienda { get; set; }

    [Required(ErrorMessage = "Indica si la vivienda es propia, arrendada o familiar.")]
    [Display(Name = "La vivienda es")]
    public TenenciaVivienda? TenenciaVivienda { get; set; }

    [Display(Name = "¿El arrendador permite mascotas?")]
    public bool? ArrendadorPermiteMascotas { get; set; }

    [Required(ErrorMessage = "Indica cuántas personas viven en tu hogar.")]
    [Range(1, 30, ErrorMessage = "Escribe un número entre 1 y 30, contándote a ti.")]
    [Display(Name = "¿Cuántas personas viven en tu hogar, contándote a ti?")]
    public int? Convivientes { get; set; }

    [Required(ErrorMessage = "Indica si todas las personas del hogar están de acuerdo.")]
    [Display(Name = "¿Todas están de acuerdo con la adopción?")]
    public bool? ConvivientesDeAcuerdo { get; set; }

    [Required(ErrorMessage = "Indica si hay niños en tu hogar.")]
    [Display(Name = "¿Hay niños en tu hogar?")]
    public bool? NinosEnCasa { get; set; }

    /// <summary>Escenario 1: solo si hay niños en casa.</summary>
    [Display(Name = "¿Los niños han interactuado con mascotas?")]
    public bool? NinosInteractuanMascotas { get; set; }

    /// <summary>Escenario 2: al responderla se muestra la información de toxoplasmosis.</summary>
    [Required(ErrorMessage = "Responde esta pregunta.")]
    [Display(Name = "¿Alguien en tu hogar está embarazada o planea estarlo?")]
    public bool? EmbarazoEnHogar { get; set; }

    [Required(ErrorMessage = "Responde esta pregunta.")]
    [Display(Name = "¿Puedes cubrir su alimentación, arena, vacunas y controles veterinarios?")]
    public bool? PuedeCubrirCostos { get; set; }

    [Required(ErrorMessage = "Responde esta pregunta.")]
    [Display(Name = "¿Aceptas que la fundación haga una visita de seguimiento a tu hogar?")]
    public bool? AceptaVisita { get; set; }

    // Escenario 3: aceptaciones obligatorias para enviar
    public bool AceptaRequisito { get; set; }
    public bool AceptaContrato { get; set; }
    public bool AutorizaDatos { get; set; }
}

/// <summary>Una solicitud de adopción en "Mis adopciones".</summary>
public class SolicitudAdopcionItem
{
    public string? Codigo { get; set; }
    public string NombreEsal { get; set; } = string.Empty;
    public string SlugEsal { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public EstadoSolicitudAdopcion Estado { get; set; }
    public DateTime Fecha { get; set; }
}
