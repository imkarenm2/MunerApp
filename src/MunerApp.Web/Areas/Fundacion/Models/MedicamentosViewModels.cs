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

    /// <summary>Plazo de aviso de vencimiento de la fundación (HU-039).</summary>
    public int DiasAviso { get; set; } = Medicamento.DiasPorVencerPredeterminado;
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

/// <summary>HU-040 escenario 2: filtros del reporte de medicamentos.</summary>
public enum FiltroReporteMedicamentos
{
    Todos,
    Vencidos,
    PorVencer,
    StockBajo
}

public class FilaReporteMedicamentoItem : MedicamentoItem
{
    public string Uso { get; set; } = string.Empty;
    public string Dosis { get; set; } = string.Empty;
    public bool Vencido { get; set; }
    public bool PorVencer { get; set; }
    public bool StockBajo { get; set; }

    /// <summary>"Vencido", "Por vencer", "Stock bajo" (pueden combinarse) o "Al día".</summary>
    public string Estado { get; set; } = string.Empty;
}

/// <summary>HU-040: reporte de medicamentos con filtros y descarga en PDF.</summary>
public class ReporteMedicamentosViewModel
{
    public FiltroReporteMedicamentos Filtro { get; set; }
    public List<FilaReporteMedicamentoItem> Filas { get; set; } = new();
    public int Total { get; set; }
    public int Vencidos { get; set; }
    public int PorVencer { get; set; }
    public int StockBajo { get; set; }
    public int DiasPorVencer { get; set; }

    public static string TextoDe(FiltroReporteMedicamentos f) => f switch
    {
        FiltroReporteMedicamentos.Vencidos => "Vencidos",
        FiltroReporteMedicamentos.PorVencer => "Por vencer",
        FiltroReporteMedicamentos.StockBajo => "Stock bajo",
        _ => "Todos"
    };
}

/// <summary>HU-039: configuración de las alertas de salud.</summary>
public class AlertasSaludViewModel
{
    /// <summary>Texto y no número, para que los mensajes de validación salgan en español.</summary>
    [Display(Name = "Avisar con cuántos días de anticipación")]
    public string? DiasAvisoVencimiento { get; set; }

    public DateTime? UltimaRevision { get; set; }
}
