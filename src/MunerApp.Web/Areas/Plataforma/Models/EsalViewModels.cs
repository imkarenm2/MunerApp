using MunerApp.Web.Validacion;
using System.ComponentModel.DataAnnotations;

namespace MunerApp.Web.Areas.Plataforma.Models;

public class EsalListaItem
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string Nit { get; set; } = string.Empty;
    public string TipoEntidad { get; set; } = string.Empty;
    public bool Activa { get; set; }
    public DateTime FechaRegistro { get; set; }
    public int ModulosActivos { get; set; }
    public int Usuarios { get; set; }
}

public class EsalDatosViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Ingresa el nombre de la fundación.")]
    [StringLength(150, ErrorMessage = "El nombre no puede superar {1} caracteres.")]
    [Display(Name = "Nombre de la fundación")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa el NIT.")]
    [RegularExpression(@"^[0-9]{6,12}(-[0-9])?$", ErrorMessage = "Escribe el NIT solo con números y el dígito de verificación, por ejemplo 900123456-7.")]
    [Display(Name = "NIT")]
    public string Nit { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona el tipo de entidad.")]
    [Display(Name = "Tipo de entidad")]
    public string TipoEntidad { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa el correo de contacto.")]
    [Correo]
    [Display(Name = "Correo de contacto de la fundación")]
    public string CorreoContacto { get; set; } = string.Empty;
}

public class EsalCrearViewModel : EsalDatosViewModel
{
    [Required(ErrorMessage = "Ingresa el nombre del administrador.")]
    [StringLength(150)]
    [Display(Name = "Nombre del administrador principal")]
    public string AdminNombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa el correo del administrador.")]
    [Correo]
    [Display(Name = "Correo del administrador principal")]
    public string AdminEmail { get; set; } = string.Empty;
}

public class ModulosEsalViewModel
{
    public int EsalId { get; set; }
    public string EsalNombre { get; set; } = string.Empty;
    public List<ModuloItem> Modulos { get; set; } = new();
}

public class ModuloItem
{
    public int ModuloId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; }
}
