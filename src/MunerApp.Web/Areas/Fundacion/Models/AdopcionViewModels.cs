using System.ComponentModel.DataAnnotations;

namespace MunerApp.Web.Areas.Fundacion.Models;

/// <summary>
/// Configuración del proceso de adopción de la fundación: recomendaciones (HU-029),
/// valor del aporte e información de toxoplasmosis (HU-032).
/// </summary>
public class ConfigAdopcionViewModel
{
    [Required(ErrorMessage = "Escribe al menos una recomendación.")]
    [StringLength(3000, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Recomendaciones y responsabilidades")]
    public string Recomendaciones { get; set; } = string.Empty;

    /// <summary>En pesos, como texto ("100.000"): se interpreta con Formatos.LeerPesos.</summary>
    [Required(ErrorMessage = "Ingresa el valor del aporte.")]
    [Display(Name = "Aporte al adoptar (esterilización y vacuna)")]
    public string ValorAporte { get; set; } = string.Empty;

    [StringLength(1500, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Mensaje sobre toxoplasmosis")]
    public string? MensajeToxoplasmosis { get; set; }

    [Display(Name = "Imagen sobre toxoplasmosis (opcional)")]
    public IFormFile? ImagenToxoplasmosis { get; set; }

    public bool QuitarImagen { get; set; }

    // Datos para mostrar
    public bool Personalizadas { get; set; }
    public DateTime? FechaActualizacion { get; set; }
    public string? Slug { get; set; }
    public string? ImagenToxoplasmosisUrl { get; set; }
}
