using System.ComponentModel.DataAnnotations;
using MunerApp.Domain.Enums;
using MunerApp.Web.Validacion;

namespace MunerApp.Web.Areas.Fundacion.Models;

/// <summary>HU-009: perfil institucional.</summary>
public class PerfilEsalViewModel
{
    [Required(ErrorMessage = "Ingresa el nombre de la fundación.")]
    [StringLength(150, ErrorMessage = "El nombre no puede superar {1} caracteres.")]
    [Display(Name = "Nombre de la fundación")]
    public string Nombre { get; set; } = string.Empty;

    // Misma regla que en el registro de la ESAL (EsalDatosViewModel). Si se cambia allá, cámbienla aquí también.
    [Required(ErrorMessage = "Ingresa el NIT.")]
    [RegularExpression(@"^[0-9]{6,12}(-[0-9])?$", ErrorMessage = "Escribe el NIT solo con números y el dígito de verificación, por ejemplo 900123456-7.")]
    [Display(Name = "NIT")]
    public string Nit { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Descripción corta")]
    public string? DescripcionCorta { get; set; }

    [StringLength(4000, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Nuestra historia")]
    public string? Historia { get; set; }

    [Required(ErrorMessage = "Ingresa la misión de la fundación.")]
    [StringLength(1000, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Misión")]
    public string? Mision { get; set; }

    [StringLength(1000, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Visión")]
    public string? Vision { get; set; }

    [StringLength(100)]
    [Display(Name = "Ciudad o municipio")]
    public string? Ciudad { get; set; }

    [RegularExpression(@"^\+?[0-9 ]{7,16}$", ErrorMessage = "Escribe solo números, por ejemplo 3001234567.")]
    [Display(Name = "Teléfono o WhatsApp")]
    public string? Telefono { get; set; }

    [Required(ErrorMessage = "Ingresa el correo de contacto.")]
    [Correo]
    [Display(Name = "Correo de contacto")]
    public string CorreoContacto { get; set; } = string.Empty;

    [Display(Name = "Pausar la recepción de voluntarios")]
    public bool VoluntariadoPausado { get; set; }

    [Display(Name = "Logo")]
    public IFormFile? Logo { get; set; }

    [Display(Name = "Quitar el logo actual")]
    public bool QuitarLogo { get; set; }

    // Solo lectura
    public string? LogoUrl { get; set; }
    public string? Slug { get; set; }
    public List<FotoItem> Fotos { get; set; } = new();
    public DateTime? FechaActualizacion { get; set; }
}

public record FotoItem(int Id, string Url);

/// <summary>HU-012: documentos de transparencia.</summary>
public class DocumentoItem
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public CategoriaDocumento Categoria { get; set; }
    public string? Descripcion { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public long TamanoBytes { get; set; }
    public bool Visible { get; set; }
    public DateTime FechaPublicacion { get; set; }
    public DateTime? FechaOcultado { get; set; }
}

public class DocumentoFormViewModel
{
    [Required(ErrorMessage = "Ingresa el título del documento.")]
    [StringLength(150, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Título")]
    public string Titulo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona la categoría.")]
    [Display(Name = "Categoría")]
    public CategoriaDocumento? Categoria { get; set; }

    [StringLength(300, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Descripción (opcional)")]
    public string? Descripcion { get; set; }

    [Display(Name = "Archivo")]
    public IFormFile? Archivo { get; set; }
}

/// <summary>HU-013: datos oficiales para donar.</summary>
public class DatosDonacionViewModel
{
    [Required(ErrorMessage = "Ingresa el titular de la cuenta.")]
    [StringLength(150)]
    [Display(Name = "Titular")]
    public string Titular { get; set; } = string.Empty;

    /// <summary>Siempre es el NIT de la fundación: la cuenta debe estar a su nombre (se asigna en el servidor).</summary>
    [Display(Name = "NIT del titular")]
    public string DocumentoTitular { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa la entidad (banco o billetera).")]
    [StringLength(100)]
    [Display(Name = "Entidad")]
    public string Entidad { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona el tipo.")]
    [Display(Name = "Tipo")]
    public TipoCuentaDonacion? TipoCuenta { get; set; }

    [Display(Name = "Tipo de llave")]
    public TipoLlave? TipoLlave { get; set; }

    [Required(ErrorMessage = "Ingresa la llave o el número de cuenta.")]
    [StringLength(60)]
    [Display(Name = "Llave o número de cuenta")]
    public string Numero { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Instrucciones para el donante (opcional)")]
    public string? Instrucciones { get; set; }

    public bool Configurado { get; set; }
    public DateTime? FechaActualizacion { get; set; }
    public string? NitEsal { get; set; }
    public string? Slug { get; set; }

    public static readonly string[] Bancos =
        { "Bancolombia", "Banco de Bogotá", "Davivienda", "BBVA", "Banco de Occidente", "Banco Popular", "Banco Caja Social", "AV Villas", "Banco Agrario", "Itaú", "Scotiabank Colpatria", "Banco Falabella", "Nu" };

    public static readonly string[] Billeteras = { "Nequi", "Daviplata", "Dale!", "Movii", "Lulo Bank" };
}

/// <summary>HU-015: bandeja de donaciones de la fundación.</summary>
public class DonacionesEsalViewModel
{
    public EstadoDonacion Estado { get; set; }
    public Dictionary<EstadoDonacion, int> Conteos { get; set; } = new();
    public List<DonacionEsalItem> Donaciones { get; set; } = new();
    public decimal TotalConfirmado { get; set; }
}

public class DonacionEsalItem
{
    /// <summary>Si es un aporte de apadrinamiento (HU-021), el nombre del apadrinado.</summary>
    public string? Apadrinado { get; set; }

    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Donante { get; set; } = string.Empty;
    public string CorreoDonante { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public DateTime FechaTransferencia { get; set; }
    public DateTime FechaReporte { get; set; }
    public EstadoDonacion Estado { get; set; }
}

public class DonacionEsalDetalleViewModel : DonacionEsalItem
{
    public string MedioPago { get; set; } = string.Empty;
    public string? ReferenciaPago { get; set; }
    public string? Mensaje { get; set; }
    public string? MotivoRechazo { get; set; }
    public DateTime? FechaRevision { get; set; }
    public string? RevisadoPor { get; set; }
    public bool SoporteEsPdf { get; set; }

    /// <summary>HU-044: las donaciones en línea las confirma el aviso de Wompi, no la fundación.</summary>
    public bool EnLinea { get; set; }
    public string? TransaccionPasarelaId { get; set; }
}

/// <summary>HU-035: postulaciones recibidas (solo consulta; la aprobación llega en el Sprint 6).</summary>
public class PostulacionEsalItem
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public TipoPostulacion Tipo { get; set; }
    public string Disponibilidad { get; set; } = string.Empty;
    public string? Motivacion { get; set; }
    public string? Institucion { get; set; }
    public string? Programa { get; set; }
    public int? Semestre { get; set; }
    public bool TieneSoporte { get; set; }
    public EstadoPostulacion Estado { get; set; }
    public DateTime Fecha { get; set; }
}
