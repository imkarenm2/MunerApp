using MunerApp.Domain.Common;

namespace MunerApp.Domain.Entities;

/// <summary>Mensaje de un chat de la tienda (HU-025, HU-026).</summary>
public class MensajeTienda : IPerteneceAEsal
{
    public int Id { get; set; }
    public int EsalId { get; set; }
    public int ConversacionId { get; set; }
    public string AutorId { get; set; } = string.Empty;

    /// <summary>Lo escribió un administrador de la fundación (true) o el donante (false).</summary>
    public bool DeLaFundacion { get; set; }

    public string Texto { get; set; } = string.Empty;
    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    public ConversacionTienda? Conversacion { get; set; }
}
