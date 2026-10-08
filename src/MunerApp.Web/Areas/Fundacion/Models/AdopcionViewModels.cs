using System.ComponentModel.DataAnnotations;

namespace MunerApp.Web.Areas.Fundacion.Models;

/// <summary>HU-029: la fundación define las recomendaciones que se leen antes del formulario de adopción.</summary>
public class ConfigRecomendacionesViewModel
{
    [Required(ErrorMessage = "Escribe al menos una recomendación.")]
    [StringLength(3000, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Recomendaciones y responsabilidades")]
    public string Recomendaciones { get; set; } = string.Empty;

    // Datos para mostrar
    public bool Personalizadas { get; set; }
    public DateTime? FechaActualizacion { get; set; }
    public string? Slug { get; set; }
}
