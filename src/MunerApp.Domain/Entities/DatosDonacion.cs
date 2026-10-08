using MunerApp.Domain.Common;
using MunerApp.Domain.Enums;

namespace MunerApp.Domain.Entities;

/// <summary>
/// Datos oficiales para recibir donaciones por llave o transferencia (HU-013).
/// El dinero va directo a la cuenta de la fundación; MunerApp solo muestra los datos.
/// </summary>
public class DatosDonacion : IPerteneceAEsal
{
    public int EsalId { get; set; }
    public string Titular { get; set; } = string.Empty;
    public string DocumentoTitular { get; set; } = string.Empty;
    public string Entidad { get; set; } = string.Empty;
    public TipoCuentaDonacion TipoCuenta { get; set; }

    /// <summary>Solo cuando TipoCuenta es Llave.</summary>
    public TipoLlave? TipoLlave { get; set; }
    public string Numero { get; set; } = string.Empty;
    public string? Instrucciones { get; set; }
    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;

    public Esal? Esal { get; set; }
}
