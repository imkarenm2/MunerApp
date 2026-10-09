using System.ComponentModel.DataAnnotations;
using MunerApp.Domain.Enums;

namespace MunerApp.Web.Areas.Fundacion.Models;

/// <summary>HU-041: formulario de una causa de recaudación (publicar y editar).</summary>
public class CausaFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Ingresa el título de la causa.")]
    [StringLength(120, MinimumLength = 5, ErrorMessage = "El título debe tener entre {2} y {1} caracteres.")]
    [Display(Name = "Título")]
    public string Titulo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Describe para qué se recauda el dinero.")]
    [StringLength(3000, MinimumLength = 30, ErrorMessage = "La descripción debe tener entre {2} y {1} caracteres.")]
    [Display(Name = "Descripción")]
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>Se recibe como texto para aceptar "2.500.000" o "$ 2,500,000" (pesos sin decimales).</summary>
    [Required(ErrorMessage = "Ingresa la meta en pesos.")]
    [Display(Name = "Meta de recaudo")]
    public string Meta { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa la fecha límite.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha límite")]
    public DateTime? FechaLimite { get; set; }

    [Display(Name = "Fotos")]
    public List<IFormFile>? Fotos { get; set; }

    /// <summary>Por qué necesitan la causa: la lee el superadministrador antes de aprobarla.</summary>
    [StringLength(1000, ErrorMessage = "La justificación puede tener máximo {1} caracteres.")]
    [Display(Name = "¿Por qué necesitan esta causa?")]
    public string? Justificacion { get; set; }

    /// <summary>true cuando la causa la crea o corrige la fundación y debe pasar por aprobación.</summary>
    public bool PideJustificacion { get; set; }

    /// <summary>Si el superadministrador la rechazó, el motivo (para que la fundación la corrija).</summary>
    public string? MotivoRechazo { get; set; }

    // Solo lectura al editar (no se envían)
    public decimal Recaudado { get; set; }
    public IReadOnlyList<FotoCausaItem> FotosActuales { get; set; } = Array.Empty<FotoCausaItem>();
}

public record FotoCausaItem(int Id, string Url);

public class CausaItem
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public decimal Meta { get; set; }
    public decimal Recaudado { get; set; }
    public DateTime FechaLimite { get; set; }
    public EstadoCausa Estado { get; set; }
    public bool Cerrada { get; set; }
    public int Porcentaje { get; set; }
    public int DiasRestantes { get; set; }
    public string? FotoUrl { get; set; }
    public string? MotivoRechazo { get; set; }
    public bool EnRevision => Estado is EstadoCausa.PorAprobar or EstadoCausa.Rechazada;
}

public class CausasIndexViewModel
{
    public IReadOnlyList<CausaItem> Causas { get; set; } = Array.Empty<CausaItem>();
    public string? Slug { get; set; }

    /// <summary>Solo el administrador principal publica y edita (es contenido público de la fundación).</summary>
    public bool PuedeGestionar { get; set; }
}
