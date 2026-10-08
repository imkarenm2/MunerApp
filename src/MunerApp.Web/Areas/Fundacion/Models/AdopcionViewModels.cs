using System.ComponentModel.DataAnnotations;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;

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

/// <summary>HU-033: panel de solicitudes de adopción por estado.</summary>
public class SolicitudesAdopcionViewModel
{
    public EstadoSolicitudAdopcion Estado { get; set; }
    public Dictionary<EstadoSolicitudAdopcion, int> Conteos { get; set; } = new();
    public List<SolicitudAdopcionEsalItem> Solicitudes { get; set; } = new();
    public bool PuedeConfigurar { get; set; }
}

public class SolicitudAdopcionEsalItem
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Ciudad { get; set; } = string.Empty;
    public DateTime FechaEnvio { get; set; }
    public EstadoSolicitudAdopcion Estado { get; set; }
}

/// <summary>HU-033 escenario 1: detalle completo de una solicitud.</summary>
public class SolicitudAdopcionDetalleViewModel
{
    public SolicitudAdopcion Solicitud { get; set; } = null!;
    public string Correo { get; set; } = string.Empty;
    public string? RevisadoPor { get; set; }
    public bool CarneEsPdf { get; set; }
}
