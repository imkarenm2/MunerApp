using MunerApp.Web.Validacion;
using System.ComponentModel.DataAnnotations;

namespace MunerApp.Web.Models.Cuenta;

public class InicioSesionViewModel
{
    [Required(ErrorMessage = "Ingresa tu correo.")]
    [Correo(SugerirCorrecciones = false)]
    [Display(Name = "Correo electrónico")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa tu contraseña.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string Contrasena { get; set; } = string.Empty;

    [Display(Name = "Recordarme")]
    public bool Recordarme { get; set; }
}
