using MunerApp.Domain.Common;
using MunerApp.Domain.Enums;

namespace MunerApp.Domain.Entities;

/// <summary>
/// Donación en especie: el donante ofrece artículos y la fundación confirma cuántos recibió.
/// Con la recepción se genera el comprobante y queda como evidencia para la transparencia y la veeduría.
/// </summary>
public class DonacionEspecie : IPerteneceAEsal
{
    public int Id { get; set; }
    public int EsalId { get; set; }

    /// <summary>Código que ve el donante, por ejemplo ESP-2026-000012.</summary>
    public string Codigo { get; set; } = string.Empty;
    public string DonanteId { get; set; } = string.Empty;

    /// <summary>Necesidad publicada a la que responde. Null si el donante ofrece algo que no estaba en la lista.</summary>
    public int? NecesidadId { get; set; }

    public string Articulo { get; set; } = string.Empty;
    public CategoriaEspecie Categoria { get; set; }
    public string Unidad { get; set; } = string.Empty;
    public int CantidadOfrecida { get; set; }

    public FormaEntregaEspecie FormaEntrega { get; set; }
    public DateTime FechaEntregaEstimada { get; set; }

    /// <summary>Solo si la fundación recoge a domicilio.</summary>
    public string? DireccionRecogida { get; set; }
    public string? Mensaje { get; set; }

    public EstadoDonacionEspecie Estado { get; set; } = EstadoDonacionEspecie.Ofrecida;
    public DateTime FechaOferta { get; set; } = DateTime.UtcNow;

    // ---------- Recepción (la registra la fundación) ----------
    public int? CantidadRecibida { get; set; }
    public DateTime? FechaRecepcion { get; set; }

    /// <summary>Para alimentos y medicamentos: fecha de vencimiento de lo recibido.</summary>
    public DateTime? FechaVencimiento { get; set; }
    public string? ObservacionesRecepcion { get; set; }

    /// <summary>Foto de lo recibido: es evidencia pública de transparencia (no muestra datos del donante).</summary>
    public string? FotoRecepcionRuta { get; set; }
    public string? RecibidaPorId { get; set; }

    /// <summary>Si no se recibió o el donante la canceló, por qué.</summary>
    public string? MotivoNoRecibida { get; set; }

    public Esal? Esal { get; set; }
    public NecesidadEspecie? Necesidad { get; set; }

    public bool Pendiente => Estado == EstadoDonacionEspecie.Ofrecida;
}
