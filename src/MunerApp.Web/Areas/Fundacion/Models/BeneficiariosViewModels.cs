using System.ComponentModel.DataAnnotations;
using MunerApp.Domain.Enums;
using MunerApp.Web.Validacion;

namespace MunerApp.Web.Areas.Fundacion.Models;

/// <summary>HU-017: formulario de la hoja de vida (registro y edición).</summary>
public class BeneficiarioFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Ingresa el nombre.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar {1} caracteres.")]
    [Display(Name = "Nombre")]
    public string Nombre { get; set; } = string.Empty;

    [Range(0, 30, ErrorMessage = "Escribe los años entre 0 y 30.")]
    [Display(Name = "Años")]
    public int EdadAnios { get; set; }

    [Range(0, 11, ErrorMessage = "Escribe los meses entre 0 y 11.")]
    [Display(Name = "Meses")]
    public int EdadMeses { get; set; }

    [Required(ErrorMessage = "Selecciona el sexo.")]
    [Display(Name = "Sexo")]
    public SexoBeneficiario? Sexo { get; set; }

    [Required(ErrorMessage = "Ingresa el color.")]
    [StringLength(60, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Color")]
    public string Color { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa la fecha de rescate.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha de rescate")]
    public DateTime? FechaRescate { get; set; }

    [Display(Name = "Foto")]
    public IFormFile? Foto { get; set; }

    [Display(Name = "Quitar la foto actual")]
    public bool QuitarFoto { get; set; }

    // Solo lectura (no se envían)
    public bool TieneFoto { get; set; }
}

public class BeneficiarioItem
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public DateTime FechaNacimiento { get; set; }
    public SexoBeneficiario Sexo { get; set; }
    public string Color { get; set; } = string.Empty;
    public EstadoBeneficiario Estado { get; set; }
    public bool TieneFoto { get; set; }

    /// <summary>true si está "Adoptado" y todavía no se guardaron los datos del adoptante.</summary>
    public bool FaltaAdoptante { get; set; }
}

public class BeneficiariosIndexViewModel
{
    public IReadOnlyList<BeneficiarioItem> Beneficiarios { get; set; } = Array.Empty<BeneficiarioItem>();
}

public record CambioEstadoItem(EstadoBeneficiario Estado, DateTime Fecha, string? Nota, string? Responsable);

public class AdoptanteItem
{
    public string Nombre { get; set; } = string.Empty;
    public string Documento { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string? Correo { get; set; }
    public string Ciudad { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public DateTime FechaAdopcion { get; set; }
    public string? Observaciones { get; set; }
}

public class BeneficiarioDetalleViewModel : BeneficiarioItem
{
    public DateTime FechaRescate { get; set; }
    public DateTime FechaRegistro { get; set; }
    public string? RegistradoPor { get; set; }
    public IReadOnlyList<CambioEstadoItem> Historial { get; set; } = Array.Empty<CambioEstadoItem>();
    public AdoptanteItem? Adoptante { get; set; }
}

public class CambioEstadoViewModel
{
    [Required(ErrorMessage = "Selecciona el nuevo estado.")]
    public EstadoBeneficiario? Estado { get; set; }

    [StringLength(300, ErrorMessage = "La nota no puede superar {1} caracteres.")]
    public string? Nota { get; set; }
}

/// <summary>HU-017, escenario 3: datos de quien adopta al beneficiario.</summary>
public class AdoptanteFormViewModel
{
    // Para mostrar (no se envían)
    public int BeneficiarioId { get; set; }
    public string NombreBeneficiario { get; set; } = string.Empty;
    public DateTime FechaRescate { get; set; }
    public bool YaRegistrado { get; set; }

    [Required(ErrorMessage = "Ingresa el nombre del adoptante.")]
    [StringLength(150, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Nombre completo")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa el número de cédula.")]
    [RegularExpression(@"^[0-9]{6,12}$", ErrorMessage = "Escribe la cédula solo con números, sin puntos ni espacios.")]
    [Display(Name = "Cédula")]
    public string Documento { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa el teléfono o celular.")]
    [RegularExpression(@"^\+?[0-9 ]{7,16}$", ErrorMessage = "Escribe solo números, por ejemplo 3001234567.")]
    [Display(Name = "Celular")]
    public string Telefono { get; set; } = string.Empty;

    [Correo]
    [Display(Name = "Correo electrónico (opcional)")]
    public string? Correo { get; set; }

    [Required(ErrorMessage = "Ingresa la ciudad o municipio.")]
    [StringLength(100, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Ciudad o municipio")]
    public string Ciudad { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa la dirección.")]
    [StringLength(200, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Dirección")]
    public string Direccion { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa la fecha de adopción.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha de adopción")]
    public DateTime? FechaAdopcion { get; set; }

    [StringLength(500, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Observaciones (opcional)")]
    public string? Observaciones { get; set; }
}
