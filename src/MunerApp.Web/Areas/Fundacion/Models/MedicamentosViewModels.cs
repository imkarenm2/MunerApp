using System.ComponentModel.DataAnnotations;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;

namespace MunerApp.Web.Areas.Fundacion.Models;

/// <summary>HU-037 escenario 1: registro y edición de un medicamento o insumo.</summary>
public class MedicamentoFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Ingresa el nombre comercial.")]
    [StringLength(150, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Nombre comercial")]
    public string NombreComercial { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa el principio activo.")]
    [StringLength(150, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Principio activo")]
    public string PrincipioActivo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa la fecha de vencimiento.")]
    [Display(Name = "Fecha de vencimiento")]
    public DateTime? FechaVencimiento { get; set; }

    [Required(ErrorMessage = "Cuéntanos para qué se usa.")]
    [StringLength(300, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Uso")]
    public string Uso { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona la vía de administración.")]
    [Display(Name = "Vía de administración")]
    public ViaAdministracion? Via { get; set; }

    [Required(ErrorMessage = "Ingresa la dosis.")]
    [StringLength(150, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Dosis")]
    public string Dosis { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona la presentación.")]
    [Display(Name = "Se cuenta en")]
    public PresentacionMedicamento? Presentacion { get; set; }

    /// <summary>Solo al registrar: después la cantidad cambia con entradas y usos (escenario 2).</summary>
    [Display(Name = "Cantidad disponible")]
    public string? Cantidad { get; set; }

    [Display(Name = "Cantidad mínima (opcional)")]
    public string? CantidadMinima { get; set; }
}

public class MedicamentoItem
{
    public int Id { get; set; }
    public string NombreComercial { get; set; } = string.Empty;
    public string PrincipioActivo { get; set; } = string.Empty;
    public DateTime FechaVencimiento { get; set; }
    public decimal Cantidad { get; set; }
    public decimal? CantidadMinima { get; set; }
    public PresentacionMedicamento Presentacion { get; set; }
    public ViaAdministracion Via { get; set; }
}

public class MedicamentosIndexViewModel
{
    public string? Busqueda { get; set; }
    public List<MedicamentoItem> Medicamentos { get; set; } = new();
}

/// <summary>HU-037 escenario 2: entrada o uso de un medicamento.</summary>
public class MovimientoMedicamentoViewModel
{
    public TipoMovimientoMedicamento? Tipo { get; set; }
    public string? Cantidad { get; set; }
    public int? BeneficiarioId { get; set; }
    public string? Nota { get; set; }
}

public class MovimientoMedicamentoItem
{
    public TipoMovimientoMedicamento Tipo { get; set; }
    public decimal Cantidad { get; set; }
    public decimal CantidadResultante { get; set; }
    public DateTime Fecha { get; set; }
    public string? Beneficiario { get; set; }
    public string? Nota { get; set; }
    public string? RegistradoPor { get; set; }
}

public class MedicamentoDetalleViewModel
{
    public Medicamento Medicamento { get; set; } = null!;
    public List<MovimientoMedicamentoItem> Movimientos { get; set; } = new();

    /// <summary>Beneficiarios de la fundación, para asociar un uso (opcional).</summary>
    public List<BeneficiarioOpcion> Beneficiarios { get; set; } = new();
}
