using System.ComponentModel.DataAnnotations;
using MunerApp.Web.Validacion;

namespace MunerApp.Web.Models.Publico;

/// <summary>Solicitud de una fundación que quiere estar en MunerApp (botón "Quiero participar" del inicio).</summary>
public class ParticiparViewModel
{
    [Required(ErrorMessage = "Ingresa el nombre de la fundación.")]
    [StringLength(150)]
    [Display(Name = "Nombre de la fundación")]
    public string NombreFundacion { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona a qué se dedica la fundación.")]
    [Display(Name = "¿A qué se dedica?")]
    public string TipoEntidad { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona el municipio.")]
    [StringLength(100)]
    [Display(Name = "Municipio")]
    public string Ciudad { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa tu nombre.")]
    [StringLength(150)]
    [Display(Name = "Tu nombre")]
    public string NombreContacto { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa tu correo.")]
    [Correo]
    [Display(Name = "Correo")]
    public string Correo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa tu celular.")]
    [RegularExpression(@"^\+?[0-9 ]{7,16}$", ErrorMessage = "Escribe solo números, por ejemplo 3001234567.")]
    [Display(Name = "Celular")]
    public string Telefono { get; set; } = string.Empty;

    [StringLength(300, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Cuéntanos qué necesitan (opcional)")]
    public string? Mensaje { get; set; }

    /// <summary>Campo oculto contra robots: una persona nunca lo llena.</summary>
    public string? SitioWeb { get; set; }
}
