using System.ComponentModel.DataAnnotations;
using MunerApp.Domain.Enums;

namespace MunerApp.Web.Areas.Fundacion.Models;

/// <summary>HU-023: formulario de un producto de la tienda (crear y editar).</summary>
public class ProductoFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Ingresa el nombre del producto.")]
    [StringLength(120, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre {2} y {1} caracteres.")]
    [Display(Name = "Nombre")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "Describe el producto.")]
    [StringLength(2000, MinimumLength = 10, ErrorMessage = "La descripción debe tener entre {2} y {1} caracteres.")]
    [Display(Name = "Descripción")]
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>Se recibe como texto para aceptar "35.000" o "$ 35,000" (pesos sin decimales).</summary>
    [Required(ErrorMessage = "Ingresa el precio en pesos.")]
    [Display(Name = "Precio")]
    public string Precio { get; set; } = string.Empty;

    [StringLength(60, ErrorMessage = "La categoría puede tener máximo {1} caracteres.")]
    [Display(Name = "Categoría (opcional)")]
    public string? Categoria { get; set; }

    [Display(Name = "Estado")]
    public EstadoProducto Estado { get; set; } = EstadoProducto.Disponible;

    [Display(Name = "Fotos")]
    public List<IFormFile>? Fotos { get; set; }

    // Solo lectura al editar (no se envían)
    public IReadOnlyList<FotoProductoItem> FotosActuales { get; set; } = Array.Empty<FotoProductoItem>();

    /// <summary>Categorías que ya usa la fundación, para sugerirlas.</summary>
    public IReadOnlyList<string> CategoriasExistentes { get; set; } = Array.Empty<string>();
}

public record FotoProductoItem(int Id, string Url);

public class ProductoItem
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public string? Categoria { get; set; }
    public EstadoProducto Estado { get; set; }
    public string? FotoUrl { get; set; }
    public DateTime FechaCreacion { get; set; }
}

public class ProductosIndexViewModel
{
    public IReadOnlyList<ProductoItem> Productos { get; set; } = Array.Empty<ProductoItem>();
    public string? Slug { get; set; }
    public EstadoProducto? Filtro { get; set; }
    public int Disponibles { get; set; }
    public int Agotados { get; set; }
    public int Ocultos { get; set; }

    /// <summary>Solo el administrador principal crea y edita productos (es contenido público de la fundación).</summary>
    public bool PuedeGestionar { get; set; }
}
