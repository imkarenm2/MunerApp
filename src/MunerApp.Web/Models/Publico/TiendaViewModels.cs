using MunerApp.Domain.Enums;

namespace MunerApp.Web.Models.Publico;

/// <summary>Tarjeta de un producto en el catálogo público (HU-024).</summary>
public class ProductoTarjeta
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public string? Categoria { get; set; }
    public bool Agotado { get; set; }
    public string? FotoUrl { get; set; }

    public string SlugEsal { get; set; } = string.Empty;
    public string NombreEsal { get; set; } = string.Empty;

    /// <summary>En la tienda general (todas las fundaciones) la tarjeta muestra de qué fundación es.</summary>
    public bool MostrarFundacion { get; set; }

    public string Url => $"/fundaciones/{SlugEsal}/tienda/{Id}";
}

/// <summary>Catálogo de una fundación (/fundaciones/{slug}/tienda) o de todas (/tienda).</summary>
public class CatalogoViewModel
{
    /// <summary>Null en la tienda general.</summary>
    public string? Slug { get; set; }
    public string? NombreEsal { get; set; }
    public string? LogoUrl { get; set; }

    public IReadOnlyList<ProductoTarjeta> Productos { get; set; } = Array.Empty<ProductoTarjeta>();
    public IReadOnlyList<string> Categorias { get; set; } = Array.Empty<string>();
    public string? Categoria { get; set; }
    public string? Busqueda { get; set; }
}

public class ProductoDetalleViewModel
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public string? Categoria { get; set; }
    public EstadoProducto Estado { get; set; }
    public IReadOnlyList<string> Fotos { get; set; } = Array.Empty<string>();

    public string SlugEsal { get; set; } = string.Empty;
    public string NombreEsal { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }

    /// <summary>Otros productos visibles de la misma fundación.</summary>
    public IReadOnlyList<ProductoTarjeta> Relacionados { get; set; } = Array.Empty<ProductoTarjeta>();

    public bool Agotado => Estado == EstadoProducto.Agotado;
}
