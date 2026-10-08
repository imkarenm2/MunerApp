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

    /// <summary>Aparece en la opción "Apadrinar" del perfil público (HU-020).</summary>
    public bool Apadrinable { get; set; }
}

public class BeneficiariosIndexViewModel
{
    public IReadOnlyList<BeneficiarioItem> Beneficiarios { get; set; } = Array.Empty<BeneficiarioItem>();

    // Filtros (HU-018)
    public EstadoBeneficiario? Estado { get; set; }
    public string? Busqueda { get; set; }

    /// <summary>Cantidad de beneficiarios por estado, sin importar los filtros.</summary>
    public IReadOnlyDictionary<EstadoBeneficiario, int> Conteos { get; set; } = new Dictionary<EstadoBeneficiario, int>();

    public int Total { get; set; }
    public int Pagina { get; set; } = 1;
    public int TotalPaginas { get; set; } = 1;

    /// <summary>true para administradores (registrar y editar); los voluntarios solo consultan.</summary>
    public bool PuedeGestionar { get; set; }

    public int TotalGeneral => Conteos.Values.Sum();
    public bool HayFiltros => Estado is not null || !string.IsNullOrEmpty(Busqueda);
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
    public bool PuedeGestionar { get; set; }

    /// <summary>Administrador principal: decide qué se publica de cada beneficiario (HU-020).</summary>
    public bool PuedePublicar { get; set; }
    public string? HistoriaPublica { get; set; }
    public decimal? AporteSugerido { get; set; }
    public bool TieneFotoPublica { get; set; }
    public string? SlugEsal { get; set; }

    /// <summary>Administradores y voluntarios de salud (HU-019).</summary>
    public bool PuedeVerClinica { get; set; }
    public int EventosClinicos { get; set; }
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

// ---------------- HU-019: historia clínica ----------------

public class EventoClinicoFormViewModel
{
    // Para mostrar (no se envían)
    public int BeneficiarioId { get; set; }
    public string NombreBeneficiario { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona el tipo de evento.")]
    [Display(Name = "Tipo de evento")]
    public TipoEventoClinico? Tipo { get; set; }

    [Required(ErrorMessage = "Ingresa la fecha del evento.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha")]
    public DateTime? Fecha { get; set; }

    [Required(ErrorMessage = "Describe el evento.")]
    [StringLength(1000, MinimumLength = 5, ErrorMessage = "La descripción debe tener entre {2} y {1} caracteres.")]
    [Display(Name = "Descripción")]
    public string Descripcion { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa quién atendió o aplicó.")]
    [StringLength(150, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Responsable")]
    public string Responsable { get; set; } = string.Empty;

    [Display(Name = "Fotos (opcional)")]
    public List<IFormFile>? Fotos { get; set; }
}

public class EventoClinicoItem
{
    public int Id { get; set; }
    public TipoEventoClinico Tipo { get; set; }
    public DateTime Fecha { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public string Responsable { get; set; } = string.Empty;
    public string? RegistradoPor { get; set; }
    public DateTime FechaRegistro { get; set; }
    public IReadOnlyList<int> FotoIds { get; set; } = Array.Empty<int>();
}

public class HistoriaClinicaViewModel
{
    public int BeneficiarioId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public EstadoBeneficiario Estado { get; set; }
    public DateTime FechaNacimiento { get; set; }
    public bool TieneFoto { get; set; }
    public IReadOnlyList<EventoClinicoItem> Eventos { get; set; } = Array.Empty<EventoClinicoItem>();
}

// ---------------- HU-020: apadrinamiento ----------------

public class ApadrinamientoFormViewModel
{
    // Para mostrar (no se envían)
    public int BeneficiarioId { get; set; }
    public string NombreBeneficiario { get; set; } = string.Empty;
    public bool YaApadrinable { get; set; }
    public string? FotoPublicaUrl { get; set; }
    public bool TieneFotoInterna { get; set; }
    public string? SlugEsal { get; set; }

    [Required(ErrorMessage = "Escribe la historia que verá el público.")]
    [StringLength(600, MinimumLength = 20, ErrorMessage = "La historia debe tener entre {2} y {1} caracteres.")]
    [Display(Name = "Historia corta (la verá el público)")]
    public string HistoriaPublica { get; set; } = string.Empty;

    /// <summary>Se recibe como texto para aceptar "30.000" o "$ 30,000" (pesos sin decimales).</summary>
    [Required(ErrorMessage = "Ingresa el aporte mensual sugerido.")]
    [Display(Name = "Aporte mensual sugerido")]
    public string AporteSugerido { get; set; } = string.Empty;

    [Display(Name = "Foto pública")]
    public IFormFile? Foto { get; set; }

    [Display(Name = "Usar la foto de la hoja de vida")]
    public bool UsarFotoInterna { get; set; }
}
