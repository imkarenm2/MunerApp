using System.ComponentModel.DataAnnotations;
using MunerApp.Domain.Enums;

namespace MunerApp.Web.Areas.Fundacion.Models;

/// <summary>HU-022 escenario 1: publicar una novedad del beneficiario.</summary>
public class NovedadFormViewModel
{
    public int BeneficiarioId { get; set; }
    public string NombreBeneficiario { get; set; } = string.Empty;
    public int PadrinosActivos { get; set; }

    [Required(ErrorMessage = "Cuéntales a los padrinos qué hay de nuevo.")]
    [StringLength(2000, MinimumLength = 10, ErrorMessage = "La novedad debe tener entre {2} y {1} caracteres.")]
    [Display(Name = "¿Qué hay de nuevo?")]
    public string Texto { get; set; } = string.Empty;

    [Display(Name = "Fotos")]
    public List<IFormFile>? Fotos { get; set; }
}

public class NovedadItem
{
    public int Id { get; set; }
    public string Texto { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string? PublicadaPor { get; set; }
    public List<int> FotoIds { get; set; } = new();
}

public class NovedadesBeneficiarioViewModel
{
    public int BeneficiarioId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public EstadoBeneficiario Estado { get; set; }
    public int PadrinosActivos { get; set; }
    public bool PuedePublicar { get; set; }
    public List<NovedadItem> Novedades { get; set; } = new();
}
