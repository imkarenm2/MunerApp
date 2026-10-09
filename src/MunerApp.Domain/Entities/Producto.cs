using MunerApp.Domain.Common;
using MunerApp.Domain.Enums;

namespace MunerApp.Domain.Entities;

/// <summary>
/// Producto de la tienda de una fundación (HU-023). La plataforma no cobra en la tienda:
/// el visitante ve el catálogo (HU-024) y acuerda la compra con la fundación por chat (HU-025, HU-026).
/// </summary>
public class Producto : IPerteneceAEsal
{
    public int Id { get; set; }
    public int EsalId { get; set; }

    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>Precio en pesos.</summary>
    public decimal Precio { get; set; }

    /// <summary>Categoría libre para filtrar el catálogo, por ejemplo "Ropa" o "Accesorios".</summary>
    public string? Categoria { get; set; }

    /// <summary>Disponible, Agotado (se ve, pero no se puede pedir) u Oculto (no se ve en el catálogo).</summary>
    public EstadoProducto Estado { get; set; } = EstadoProducto.Disponible;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaActualizacion { get; set; }
    public string? CreadoPorId { get; set; }

    public Esal? Esal { get; set; }
    public ICollection<FotoProducto> Fotos { get; set; } = new List<FotoProducto>();

    public bool EsVisible => Estado != EstadoProducto.Oculto;
}
