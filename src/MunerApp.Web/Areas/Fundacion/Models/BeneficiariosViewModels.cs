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

    [Required(ErrorMessage = "Ingresa la fecha de ingreso.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha de ingreso")]
    public DateTime? FechaRescate { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de nacimiento (si se conoce)")]
    public DateTime? FechaNacimiento { get; set; }

    [StringLength(60, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Raza")]
    public string? Raza { get; set; }

    /// <summary>Texto para aceptar "3,5" o "3.5".</summary>
    [Display(Name = "Peso al ingreso (kg)")]
    public string? PesoIngreso { get; set; }

    [Display(Name = "Procedencia")]
    public Procedencia? Procedencia { get; set; }

    [StringLength(500, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Detalles de la procedencia")]
    public string? DetallesProcedencia { get; set; }

    [Display(Name = "Estado reproductivo")]
    public EstadoReproductivo? EstadoReproductivo { get; set; }

    [StringLength(300, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Señales particulares")]
    public string? SenalesParticulares { get; set; }

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
    public bool FechaNacimientoExacta { get; set; }
    public string? Raza { get; set; }
    public SexoBeneficiario Sexo { get; set; }
    public string Color { get; set; } = string.Empty;
    public EstadoBeneficiario Estado { get; set; }
    public bool TieneFoto { get; set; }

    /// <summary>true si está "Adoptado" y todavía no se guardaron los datos del adoptante.</summary>
    public bool FaltaAdoptante { get; set; }

    /// <summary>Aparece en la opción "Apadrinar" del perfil público (HU-020).</summary>
    public bool Apadrinable { get; set; }
}

/// <summary>Filtros que llegan desde el tablero del panel: gatos en la fundación con algo pendiente.</summary>
public enum PendienteBeneficiario
{
    Vacuna,
    Desparasitacion,
    Examen
}

public class BeneficiariosIndexViewModel
{
    public PendienteBeneficiario? Pendiente { get; set; }

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
    public bool HayFiltros => Estado is not null || Pendiente is not null || !string.IsNullOrEmpty(Busqueda);
}

public record CambioEstadoItem(EstadoBeneficiario Estado, DateTime Fecha, string? Nota, string? Responsable);

public class AdoptanteItem
{
    public string Nombre { get; set; } = string.Empty;
    public string? Documento { get; set; }
    public string Telefono { get; set; } = string.Empty;
    public string? Correo { get; set; }
    public string Ciudad { get; set; } = string.Empty;
    public string? Direccion { get; set; }
    public string? NumeroFormulario { get; set; }
    public string? Elaboro { get; set; }
    public DateTime FechaAdopcion { get; set; }
    public string? Observaciones { get; set; }
}

public record PadrinoItem(string Nombre, string Correo, decimal ValorMensual, DateTime FechaInicio, bool Activo, decimal TotalConfirmado);

public class BeneficiarioDetalleViewModel : BeneficiarioItem
{
    /// <summary>Padrinos del beneficiario (HU-021): solo lo ven los administradores.</summary>
    public IReadOnlyList<PadrinoItem> Padrinos { get; set; } = Array.Empty<PadrinoItem>();

    /// <summary>Administrador principal: registra, edita y cambia estados.</summary>
    public bool PuedeGestionar { get; set; }

    /// <summary>Administrador principal o de consulta: ve los datos del adoptante y los padrinos.</summary>
    public bool EsAdministrador { get; set; }

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

    // Reseña (formato de la fundación)
    public decimal? PesoIngresoKg { get; set; }
    public Procedencia? Procedencia { get; set; }
    public string? DetallesProcedencia { get; set; }
    public EstadoReproductivo? EstadoReproductivo { get; set; }
    public string? SenalesParticulares { get; set; }

    /// <summary>Examen semiológico de ingreso: solo con acceso clínico.</summary>
    public ExamenIngresoViewModel? ExamenIngreso { get; set; }

    /// <summary>Administrador principal o voluntario de salud: registra el examen de ingreso y el etograma.</summary>
    public bool PuedeRegistrarClinica { get; set; }

    public IReadOnlyList<EtogramaItem> Etogramas { get; set; } = Array.Empty<EtogramaItem>();
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

    [RegularExpression(@"^[0-9]{6,12}$", ErrorMessage = "Escribe la cédula solo con números, sin puntos ni espacios.")]
    [Display(Name = "Cédula (opcional)")]
    public string? Documento { get; set; }

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

    [StringLength(200, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Dirección (opcional)")]
    public string? Direccion { get; set; }

    [StringLength(30, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "No. de formulario de adopción")]
    public string? NumeroFormulario { get; set; }

    [Required(ErrorMessage = "Ingresa quién elaboró el registro.")]
    [StringLength(150, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Elaboró")]
    public string Elaboro { get; set; } = string.Empty;

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
    [StringLength(1000, MinimumLength = 3, ErrorMessage = "La descripción debe tener entre {2} y {1} caracteres.")]
    [Display(Name = "Descripción u observaciones")]
    public string Descripcion { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Vacuna, desparasitante o prueba")]
    public string? Producto { get; set; }

    [StringLength(100, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Laboratorio")]
    public string? Laboratorio { get; set; }

    [Display(Name = "Peso (kg)")]
    public string? Peso { get; set; }

    [StringLength(100, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Resultado")]
    public string? Resultado { get; set; }

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
    public string? Producto { get; set; }
    public string? Laboratorio { get; set; }
    public decimal? PesoKg { get; set; }
    public string? Resultado { get; set; }
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

    /// <summary>Administrador principal o voluntario de salud. El de consulta solo ve.</summary>
    public bool PuedeRegistrar { get; set; }
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


// ---------------- Examen semiológico de ingreso (formato de la fundación) ----------------

public class ExamenIngresoViewModel
{
    // Para mostrar (no se envían)
    public int BeneficiarioId { get; set; }
    public string NombreBeneficiario { get; set; } = string.Empty;
    public DateTime? FechaExamen { get; set; }
    public string? RealizadoPor { get; set; }

    [Range(1, 300, ErrorMessage = "Escribe un valor entre {1} y {2}.")]
    [Display(Name = "FR (rpm)")]
    public int? FrecuenciaRespiratoria { get; set; }

    [Range(1, 400, ErrorMessage = "Escribe un valor entre {1} y {2}.")]
    [Display(Name = "FC (lpm)")]
    public int? FrecuenciaCardiaca { get; set; }

    [Display(Name = "TLLC (seg)")]
    public string? Tllc { get; set; }

    [Display(Name = "RPC (seg)")]
    public string? Rpc { get; set; }

    [Display(Name = "Temperatura (°C)")]
    public string? Temperatura { get; set; }

    [Range(1, 5, ErrorMessage = "La condición corporal va de 1 a 5.")]
    [Display(Name = "Condición corporal")]
    public int? CondicionCorporal { get; set; }

    [StringLength(100)]
    [Display(Name = "Mucosa conjuntival")]
    public string? MucosaConjuntival { get; set; }

    [StringLength(100)]
    [Display(Name = "Mucosa oral")]
    public string? MucosaOral { get; set; }

    [StringLength(100)]
    [Display(Name = "Rectal")]
    public string? MucosaRectal { get; set; }

    [StringLength(100)]
    [Display(Name = "Vulvar / prepucial")]
    public string? MucosaVulvarPrepucial { get; set; }

    [Display(Name = "Estado de conciencia")]
    public EstadoConciencia? EstadoConciencia { get; set; }

    [StringLength(1000, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Observaciones")]
    public string? Observaciones { get; set; }

    public bool TieneDatos =>
        FrecuenciaRespiratoria is not null || FrecuenciaCardiaca is not null || !string.IsNullOrEmpty(Temperatura)
        || CondicionCorporal is not null || EstadoConciencia is not null || !string.IsNullOrEmpty(Observaciones)
        || !string.IsNullOrEmpty(MucosaConjuntival) || !string.IsNullOrEmpty(MucosaOral);
}

// ---------------- Etograma ----------------

public record EtogramaItem(int Id, MomentoEtograma Momento, DateTime Fecha, IReadOnlyList<string> Codigos, string? Observaciones, string Metodo, string? RegistradoPor);

public class EtogramaFormViewModel
{
    // Para mostrar (no se envían)
    public int BeneficiarioId { get; set; }
    public string NombreBeneficiario { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona cuándo se hizo la evaluación.")]
    [Display(Name = "Momento")]
    public MomentoEtograma? Momento { get; set; }

    [Required(ErrorMessage = "Ingresa la fecha de la evaluación.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha")]
    public DateTime? Fecha { get; set; }

    public List<string> Conductas { get; set; } = new();

    [StringLength(1000, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Observaciones")]
    public string? Observaciones { get; set; }
}
