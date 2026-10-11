using System.ComponentModel.DataAnnotations;
using MunerApp.Domain.Enums;

namespace MunerApp.Web.Models.Publico;

public class ReportarDonacionViewModel
{
    // Datos de la fundación para mostrar en la página (no se envían)
    public string Slug { get; set; } = string.Empty;
    public string NombreEsal { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string MedioPago { get; set; } = string.Empty;

    /// <summary>Si es el aporte de un apadrinamiento (HU-021), cuál es y a quién apadrina.</summary>
    public int? ApadrinamientoId { get; set; }
    public string? NombreApadrinado { get; set; }

    /// <summary>Se recibe como texto para aceptar "50.000" o "$ 50,000" (pesos sin decimales).</summary>
    [Required(ErrorMessage = "Ingresa el valor que donaste.")]
    [Display(Name = "Valor donado")]
    public string Valor { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa la fecha de la transferencia.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha de la transferencia")]
    public DateTime? FechaTransferencia { get; set; }

    [StringLength(60, ErrorMessage = "La referencia no puede superar {1} caracteres.")]
    [Display(Name = "Número de comprobante o referencia (opcional)")]
    public string? ReferenciaPago { get; set; }

    [StringLength(300, ErrorMessage = "El mensaje no puede superar {1} caracteres.")]
    [Display(Name = "Mensaje para la fundación (opcional)")]
    public string? Mensaje { get; set; }

    [Display(Name = "Soporte de la transferencia")]
    public IFormFile? Soporte { get; set; }
}

public class DonacionItem
{
    /// <summary>Si es un aporte de apadrinamiento (HU-021), el nombre del apadrinado.</summary>
    public string? Apadrinado { get; set; }

    public string Codigo { get; set; } = string.Empty;
    public string NombreEsal { get; set; } = string.Empty;
    public string? SlugEsal { get; set; }
    public string? LogoUrl { get; set; }
    public decimal Valor { get; set; }
    public DateTime FechaTransferencia { get; set; }
    public DateTime FechaReporte { get; set; }
    public EstadoDonacion Estado { get; set; }
}

public class DonacionDetalleViewModel : DonacionItem
{
    public string MedioPago { get; set; } = string.Empty;
    public string? ReferenciaPago { get; set; }
    public string? Mensaje { get; set; }
    public string? MotivoRechazo { get; set; }
    public DateTime? FechaRevision { get; set; }
    public bool SoporteEsPdf { get; set; }
    public int? ApadrinamientoId { get; set; }

    /// <summary>Pagada en línea con Wompi (HU-043): no tiene soporte y la confirma el aviso de Wompi (HU-044).</summary>
    public bool EnLinea { get; set; }
    public string? ReferenciaPasarela { get; set; }
}

public class PostulacionViewModel
{
    // Datos para mostrar
    public string Slug { get; set; } = string.Empty;
    public string NombreEsal { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public bool PermitePracticante { get; set; }

    /// <summary>Si ya tiene una postulación pendiente o pertenece al equipo, se muestra el aviso en vez del formulario.</summary>
    public string? Aviso { get; set; }

    [Display(Name = "¿Cómo quieres ayudar?")]
    public TipoPostulacion Tipo { get; set; } = TipoPostulacion.General;

    [Required(ErrorMessage = "Ingresa tu número de celular.")]
    [RegularExpression(@"^\+?[0-9 ]{7,16}$", ErrorMessage = "Escribe solo números, por ejemplo 3001234567.")]
    [Display(Name = "Celular")]
    public string Telefono { get; set; } = string.Empty;

    [Display(Name = "Días disponibles")]
    public List<string> Dias { get; set; } = new();

    [Required(ErrorMessage = "Selecciona tu jornada.")]
    [Display(Name = "Jornada")]
    public string Jornada { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "¿Por qué quieres ser voluntario? (opcional)")]
    public string? Motivacion { get; set; }

    [StringLength(150)]
    [Display(Name = "Institución educativa")]
    public string? Institucion { get; set; }

    [StringLength(150)]
    [Display(Name = "Programa académico")]
    public string? Programa { get; set; }

    [Range(1, 14, ErrorMessage = "Ingresa un semestre entre 1 y 14.")]
    [Display(Name = "Semestre")]
    public int? Semestre { get; set; }

    [Display(Name = "Soporte académico (certificado de estudio o carta de la institución)")]
    public IFormFile? SoporteAcademico { get; set; }

    public static readonly string[] DiasSemana = { "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo" };
    public static readonly string[] Jornadas = { "Mañana", "Tarde", "Todo el día", "Flexible" };
}

public class PostulacionItem
{
    public string NombreEsal { get; set; } = string.Empty;
    public string? SlugEsal { get; set; }
    public string? LogoUrl { get; set; }
    public TipoPostulacion Tipo { get; set; }
    public EstadoPostulacion Estado { get; set; }
    public DateTime Fecha { get; set; }

    /// <summary>Si la fundación la rechazó, por qué (HU-036).</summary>
    public string? MotivoRechazo { get; set; }
}
