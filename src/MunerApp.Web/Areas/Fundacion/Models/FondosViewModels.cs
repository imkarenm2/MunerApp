using System.ComponentModel.DataAnnotations;
using MunerApp.Web.Servicios;

namespace MunerApp.Web.Areas.Fundacion.Models;

public class FondosIndexViewModel
{
    public IReadOnlyList<ResumenFondo> Fondos { get; set; } = Array.Empty<ResumenFondo>();
    public IReadOnlyList<MovimientoVista> Recientes { get; set; } = Array.Empty<MovimientoVista>();
    public bool PuedeGestionar { get; set; }
    public decimal SaldoTotal => Fondos.Sum(f => f.Saldo);
    public decimal GastadoTotal => Fondos.Sum(f => f.Gastos);
    public decimal RecibidoTotal => Fondos.Sum(f => f.Donaciones);
    public ProyectoFormViewModel Proyecto { get; set; } = new();
}

public class FondoDetalleViewModel
{
    public ResumenFondo Fondo { get; set; } = null!;
    public IReadOnlyList<MovimientoVista> Movimientos { get; set; } = Array.Empty<MovimientoVista>();

    /// <summary>Fondos abiertos a los que se puede trasladar.</summary>
    public IReadOnlyList<ResumenFondo> Destinos { get; set; } = Array.Empty<ResumenFondo>();
    public bool PuedeGestionar { get; set; }
    public GastoFormViewModel Gasto { get; set; } = new();
    public TrasladoFormViewModel Traslado { get; set; } = new();
}

public class GastoFormViewModel
{
    /// <summary>Texto para aceptar "250.000" (se lee con Formatos.LeerPesos).</summary>
    [Required(ErrorMessage = "Escribe el valor del gasto.")]
    [Display(Name = "Valor")]
    public string Valor { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escribe en qué se usó el dinero.")]
    [StringLength(300, MinimumLength = 5, ErrorMessage = "Entre {2} y {1} caracteres.")]
    [Display(Name = "¿En qué se usó?")]
    public string Concepto { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escribe la fecha del gasto.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha del gasto")]
    public DateTime? Fecha { get; set; }

    [StringLength(150, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Proveedor (opcional)")]
    public string? Proveedor { get; set; }

    [Display(Name = "Soporte (factura, recibo o foto)")]
    public IFormFile? Soporte { get; set; }
}

public class TrasladoFormViewModel
{
    [Required(ErrorMessage = "Elige el fondo de destino.")]
    [Display(Name = "Hacia el fondo")]
    public string Destino { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escribe el valor a trasladar.")]
    [Display(Name = "Valor")]
    public string Valor { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escribe por qué se traslada.")]
    [StringLength(300, MinimumLength = 5, ErrorMessage = "Entre {2} y {1} caracteres.")]
    [Display(Name = "Motivo")]
    public string Concepto { get; set; } = string.Empty;
}

public class ProyectoFormViewModel
{
    [Required(ErrorMessage = "Escribe el nombre del proyecto.")]
    [StringLength(120, MinimumLength = 5, ErrorMessage = "Entre {2} y {1} caracteres.")]
    [Display(Name = "Nombre del proyecto")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Descripción (opcional)")]
    public string? Descripcion { get; set; }
}
