using System.ComponentModel.DataAnnotations;

namespace MunerApp.Web.Models.Publico;

/// <summary>Formulario previo al checkout de Wompi para donar a una causa (HU-043).</summary>
public class DonarEnLineaViewModel
{
    public int CausaId { get; set; }
    public string TituloCausa { get; set; } = string.Empty;
    public string NombreEsal { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public decimal Meta { get; set; }
    public decimal Recaudado { get; set; }
    public int Porcentaje { get; set; }

    /// <summary>Se recibe como texto para aceptar "50.000" o "$ 50,000" (pesos sin decimales).</summary>
    [Required(ErrorMessage = "Ingresa el valor que quieres donar.")]
    [Display(Name = "Valor a donar")]
    public string Valor { get; set; } = string.Empty;
}

/// <summary>
/// Lo que ve el donante al volver del checkout (HU-043). Es solo informativo: el cambio oficial
/// de la donación a Confirmada o Rechazada lo hace el aviso de Wompi (HU-044).
/// </summary>
public enum ResultadoPagoEnLinea
{
    /// <summary>La donación ya quedó confirmada por el aviso de Wompi.</summary>
    Confirmada,
    /// <summary>Wompi dice que la transacción fue aprobada, pero el aviso aún no llega.</summary>
    AprobadoPorConfirmar,
    /// <summary>La transacción sigue en proceso (por ejemplo, PSE o Nequi).</summary>
    EnProceso,
    /// <summary>Wompi la rechazó, la anuló o falló.</summary>
    NoAprobado,
    /// <summary>No se pudo consultar la transacción, o sus datos no coinciden con la donación.</summary>
    SinVerificar
}

public class ResultadoPagoViewModel
{
    public ResultadoPagoEnLinea Resultado { get; set; }
    public string CodigoDonacion { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string Referencia { get; set; } = string.Empty;
    public string? MetodoPago { get; set; }

    public string NombreEsal { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public int CausaId { get; set; }
    public string TituloCausa { get; set; } = string.Empty;

    public string UrlCausa => $"/fundaciones/{Slug}/causas/{CausaId}";
    public string UrlReintentar => $"/fundaciones/{Slug}/causas/{CausaId}/donar-en-linea";
}
