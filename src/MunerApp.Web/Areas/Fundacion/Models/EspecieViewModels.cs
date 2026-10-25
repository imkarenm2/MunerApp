using System.ComponentModel.DataAnnotations;
using MunerApp.Domain.Enums;

namespace MunerApp.Web.Areas.Fundacion.Models;

/// <summary>Panel de donaciones en especie: lo que llega y lo que la fundación necesita.</summary>
public class EspecieIndexViewModel
{
    public EstadoDonacionEspecie Estado { get; set; }
    public IReadOnlyDictionary<EstadoDonacionEspecie, int> Conteos { get; set; } = new Dictionary<EstadoDonacionEspecie, int>();
    public IReadOnlyList<DonacionEspecieItem> Donaciones { get; set; } = Array.Empty<DonacionEspecieItem>();
    public IReadOnlyList<NecesidadItem> Necesidades { get; set; } = Array.Empty<NecesidadItem>();
    public bool PuedeGestionarNecesidades { get; set; }
}

public class DonacionEspecieItem
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Donante { get; set; } = string.Empty;
    public string Articulo { get; set; } = string.Empty;
    public CategoriaEspecie Categoria { get; set; }
    public int Cantidad { get; set; }
    public string Unidad { get; set; } = string.Empty;
    public FormaEntregaEspecie FormaEntrega { get; set; }
    public DateTime FechaEntregaEstimada { get; set; }
    public DateTime FechaOferta { get; set; }
    public EstadoDonacionEspecie Estado { get; set; }
}

public class NecesidadItem
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public CategoriaEspecie Categoria { get; set; }
    public string Unidad { get; set; } = string.Empty;
    public int CantidadMeta { get; set; }
    public int Recibido { get; set; }
    public int PorRecibir { get; set; }
    public bool Urgente { get; set; }
    public bool Activa { get; set; }
    public int Porcentaje => CantidadMeta <= 0 ? 0 : Math.Min(100, (int)Math.Round(100.0 * Recibido / CantidadMeta));
}

public class NecesidadFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Escribe qué necesitan.")]
    [StringLength(120, MinimumLength = 3, ErrorMessage = "Entre {2} y {1} caracteres.")]
    [Display(Name = "¿Qué necesitan?")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona el tipo.")]
    [Display(Name = "Tipo")]
    public CategoriaEspecie? Categoria { get; set; }

    [Required(ErrorMessage = "Escribe la unidad, por ejemplo bultos de 8 kg.")]
    [StringLength(40, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Unidad")]
    public string Unidad { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escribe cuántas necesitan.")]
    [Range(1, 100000, ErrorMessage = "Escribe un número entre {1} y {2}.")]
    [Display(Name = "Cantidad que necesitan")]
    public int? CantidadMeta { get; set; }

    [StringLength(500, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Detalles (opcional)")]
    public string? Descripcion { get; set; }

    [Display(Name = "Es urgente")]
    public bool Urgente { get; set; }
}

public class DonacionEspecieDetalleViewModel
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Donante { get; set; } = string.Empty;
    public string CorreoDonante { get; set; } = string.Empty;
    public string? Necesidad { get; set; }
    public string Articulo { get; set; } = string.Empty;
    public CategoriaEspecie Categoria { get; set; }
    public int CantidadOfrecida { get; set; }
    public string Unidad { get; set; } = string.Empty;
    public FormaEntregaEspecie FormaEntrega { get; set; }
    public DateTime FechaEntregaEstimada { get; set; }
    public string? DireccionRecogida { get; set; }
    public string? Mensaje { get; set; }
    public EstadoDonacionEspecie Estado { get; set; }
    public DateTime FechaOferta { get; set; }
    public int? CantidadRecibida { get; set; }
    public DateTime? FechaRecepcion { get; set; }
    public DateTime? FechaVencimiento { get; set; }
    public string? ObservacionesRecepcion { get; set; }
    public string? FotoUrl { get; set; }
    public string? MotivoNoRecibida { get; set; }
    public string? RecibidaPor { get; set; }

    // Formulario de recepción
    public RecibirEspecieViewModel Recibir { get; set; } = new();

    // ---- Destinación del lote (qué se hizo con lo recibido) ----
    public IReadOnlyList<SalidaEspecieItem> Salidas { get; set; } = Array.Empty<SalidaEspecieItem>();
    public int Usado => Salidas.Where(s => !s.Anulado).Sum(s => s.Cantidad);
    public int Disponible => Math.Max(0, (CantidadRecibida ?? 0) - Usado);
    public UsoEspecieViewModel Uso { get; set; } = new();
}

public class RecibirEspecieViewModel
{
    [Required(ErrorMessage = "Escribe cuántas unidades llegaron.")]
    [Range(1, 100000, ErrorMessage = "Escribe un número entre {1} y {2}.")]
    [Display(Name = "¿Cuántas llegaron?")]
    public int? CantidadRecibida { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de vencimiento (si aplica)")]
    public DateTime? FechaVencimiento { get; set; }

    [StringLength(500, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Observaciones (opcional)")]
    public string? Observaciones { get; set; }

    [Display(Name = "Foto de lo recibido (opcional)")]
    public IFormFile? Foto { get; set; }
}

/// <summary>Un uso registrado de un lote de especie. La foto es pública.</summary>
public record SalidaEspecieItem(int Id, int Cantidad, string Uso, DateTime Fecha, string? FotoUrl, string? RegistradoPor,
    bool Anulado, string? MotivoAnulacion);

public class UsoEspecieViewModel
{
    [Required(ErrorMessage = "Escribe cuántas unidades se usaron.")]
    [Range(1, 100000, ErrorMessage = "Escribe un número entre {1} y {2}.")]
    [Display(Name = "¿Cuántas se usaron?")]
    public int? Cantidad { get; set; }

    [Required(ErrorMessage = "Escribe en qué se usó.")]
    [StringLength(300, MinimumLength = 5, ErrorMessage = "Entre {2} y {1} caracteres.")]
    [Display(Name = "¿En qué se usó?")]
    public string Uso { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escribe la fecha.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha")]
    public DateTime? Fecha { get; set; }

    [Display(Name = "Foto (opcional)")]
    public IFormFile? Foto { get; set; }
}

/// <summary>Inventario: lotes recibidos que todavía tienen unidades disponibles.</summary>
public class InventarioEspecieViewModel
{
    public IReadOnlyList<LoteEspecieItem> Lotes { get; set; } = Array.Empty<LoteEspecieItem>();
    public int PorVencer => Lotes.Count(l => l.PorVencer || l.Vencido);
}

public record LoteEspecieItem(int Id, string Codigo, string Articulo, CategoriaEspecie Categoria, string Unidad, int Recibido, int Disponible,
    DateTime FechaRecepcion, DateTime? Vence, bool PorVencer, bool Vencido);
