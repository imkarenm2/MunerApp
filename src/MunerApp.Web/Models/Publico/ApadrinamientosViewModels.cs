using System.ComponentModel.DataAnnotations;
using MunerApp.Domain.Enums;

namespace MunerApp.Web.Models.Publico;

/// <summary>HU-021: el donante confirma que quiere apadrinar y el valor de su aporte mensual.</summary>
public class ConfirmarApadrinamientoViewModel
{
    // Datos para mostrar (no se envían)
    public string Slug { get; set; } = string.Empty;
    public string NombreEsal { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public int BeneficiarioId { get; set; }
    public string NombreBeneficiario { get; set; } = string.Empty;
    public string Edad { get; set; } = string.Empty;
    public string? FotoUrl { get; set; }
    public decimal AporteSugerido { get; set; }

    /// <summary>false si la fundación todavía no configuró los datos para recibir aportes.</summary>
    public bool Disponible { get; set; } = true;

    /// <summary>Se recibe como texto para aceptar "30.000" o "$ 30,000" (pesos sin decimales).</summary>
    [Required(ErrorMessage = "Ingresa el valor de tu aporte mensual.")]
    [Display(Name = "Valor de tu aporte mensual")]
    public string ValorMensual { get; set; } = string.Empty;
}

public class ApadrinamientoItem
{
    public int Id { get; set; }
    public string NombreBeneficiario { get; set; } = string.Empty;
    public string? FotoUrl { get; set; }
    public string NombreEsal { get; set; } = string.Empty;
    public string? SlugEsal { get; set; }
    public string? LogoUrl { get; set; }
    public decimal ValorMensual { get; set; }
    public EstadoApadrinamiento Estado { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime? FechaCancelacion { get; set; }
}

public record AporteApadrinamiento(string Codigo, decimal Valor, DateTime FechaTransferencia, EstadoDonacion Estado);

public class ApadrinamientoDetalleViewModel : ApadrinamientoItem
{
    public int BeneficiarioId { get; set; }

    /// <summary>true si el beneficiario todavía se ofrece al público (se puede abrir su ficha).</summary>
    public bool BeneficiarioVisible { get; set; }

    // Datos oficiales para hacer el aporte (los mismos de la opción "Donar")
    public bool DatosDisponibles { get; set; }
    public string? TipoCuenta { get; set; }
    public string? Entidad { get; set; }
    public string? Numero { get; set; }
    public string? Titular { get; set; }
    public string? DocumentoTitular { get; set; }
    public string? Instrucciones { get; set; }

    public IReadOnlyList<AporteApadrinamiento> Aportes { get; set; } = Array.Empty<AporteApadrinamiento>();
    public decimal TotalConfirmado => Aportes.Where(a => a.Estado == EstadoDonacion.Confirmada).Sum(a => a.Valor);
}
