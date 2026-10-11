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

    /// <summary>HU-046: por qué se cerró y si ya se publicó la rendición de cuentas.</summary>
    public string? MotivoCierre { get; set; }
    public bool TieneRendicion { get; set; }
}

public class CausasIndexViewModel
{
    public IReadOnlyList<CausaItem> Causas { get; set; } = Array.Empty<CausaItem>();
    public string? Slug { get; set; }

    /// <summary>Solo el administrador principal publica y edita (es contenido público de la fundación).</summary>
    public bool PuedeGestionar { get; set; }
}

/// <summary>HU-046 escenario 2: rendición de cuentas de una causa cerrada.</summary>
public class RendicionFormViewModel
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public decimal Meta { get; set; }
    public decimal Recaudado { get; set; }
    public int Donaciones { get; set; }
    public int Donantes { get; set; }
    public string Motivo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Cuéntales a los donantes en qué se usó el dinero.")]
    [StringLength(4000, MinimumLength = 30, ErrorMessage = "La rendición debe tener entre {2} y {1} caracteres.")]
    [Display(Name = "¿En qué se usó el dinero?")]
    public string Texto { get; set; } = string.Empty;

    /// <summary>Texto y no número, para leerlo con Formatos.LeerPesos (acepta puntos de miles).</summary>
    [Display(Name = "Valor usado (opcional)")]
    public string? ValorEjecutado { get; set; }

    [Display(Name = "Fotos")]
    public List<IFormFile>? Fotos { get; set; }

    [Display(Name = "Soportes")]
    public List<IFormFile>? Soportes { get; set; }
}
