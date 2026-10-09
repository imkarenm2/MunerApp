using System.ComponentModel.DataAnnotations;
using MunerApp.Domain.Enums;

namespace MunerApp.Web.Areas.Fundacion.Models;

/// <summary>HU-027: formulario de una publicación del boletín (crear y editar).</summary>
public class PublicacionFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Ingresa el título.")]
    [StringLength(150, MinimumLength = 5, ErrorMessage = "El título debe tener entre {2} y {1} caracteres.")]
    [Display(Name = "Título")]
    public string Titulo { get; set; } = string.Empty;

    [Display(Name = "Categoría")]
    public CategoriaPublicacion Categoria { get; set; } = CategoriaPublicacion.Noticia;

    [StringLength(300, ErrorMessage = "El resumen puede tener máximo {1} caracteres.")]
    [Display(Name = "Resumen (opcional)")]
    public string? Resumen { get; set; }

    [Required(ErrorMessage = "Escribe el contenido de la publicación.")]
    [StringLength(8000, MinimumLength = 30, ErrorMessage = "El contenido debe tener entre {2} y {1} caracteres.")]
    [Display(Name = "Contenido")]
    public string Contenido { get; set; } = string.Empty;

    /// <summary>Fecha y hora del evento, en hora de Colombia. Obligatoria si la categoría es Evento.</summary>
    [DataType(DataType.DateTime)]
    [Display(Name = "Fecha y hora del evento")]
    public DateTime? FechaEvento { get; set; }

    [StringLength(200, ErrorMessage = "El lugar puede tener máximo {1} caracteres.")]
    [Display(Name = "Lugar del evento")]
    public string? LugarEvento { get; set; }

    [Display(Name = "Causa relacionada (opcional)")]
    public int? CausaId { get; set; }

    [Display(Name = "Imagen principal (opcional)")]
    public IFormFile? Imagen { get; set; }

    // Solo lectura
    public string? ImagenActualUrl { get; set; }
    public EstadoPublicacion Estado { get; set; }
    public IReadOnlyList<CausaOpcion> Causas { get; set; } = Array.Empty<CausaOpcion>();
}

public record CausaOpcion(int Id, string Titulo);

public class PublicacionItem
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public CategoriaPublicacion Categoria { get; set; }
    public EstadoPublicacion Estado { get; set; }
    public string? ImagenUrl { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaPublicacion { get; set; }
    public DateTime? FechaEvento { get; set; }
}

public class BoletinIndexViewModel
{
    public EstadoPublicacion Estado { get; set; }
    public int Borradores { get; set; }
    public int Publicadas { get; set; }
    public string? Slug { get; set; }
    public IReadOnlyList<PublicacionItem> Publicaciones { get; set; } = Array.Empty<PublicacionItem>();

    /// <summary>Solo el administrador principal publica en el boletín (es contenido público de la fundación).</summary>
    public bool PuedeGestionar { get; set; }
}
