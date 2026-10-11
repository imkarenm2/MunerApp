using MunerApp.Domain.Common;
using MunerApp.Domain.Enums;

namespace MunerApp.Domain.Entities;

/// <summary>
/// Chat entre un donante y una fundación sobre un producto de su tienda (HU-025).
/// Hay una sola conversación por donante y producto: si vuelve a escribir, continúa la misma.
/// La fundación la atiende desde su panel (HU-026).
/// </summary>
public class ConversacionTienda : IPerteneceAEsal
{
    public int Id { get; set; }
    public int EsalId { get; set; }
    public int ProductoId { get; set; }
    public string DonanteId { get; set; } = string.Empty;

    public EstadoConversacion Estado { get; set; } = EstadoConversacion.Abierta;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime FechaUltimoMensaje { get; set; } = DateTime.UtcNow;

    /// <summary>Hasta cuándo leyó cada lado: los mensajes posteriores del otro lado son "no leídos".</summary>
    public DateTime? UltimaLecturaDonante { get; set; }
    public DateTime? UltimaLecturaFundacion { get; set; }

    public Esal? Esal { get; set; }
    public Producto? Producto { get; set; }
    public ICollection<MensajeTienda> Mensajes { get; set; } = new List<MensajeTienda>();
}
