using System.ComponentModel.DataAnnotations;
using MunerApp.Domain.Constantes;

namespace MunerApp.Web.Areas.Fundacion.Models;

public record ModuloActivo(string Codigo, string Nombre, bool EsConfigurable);

public class PanelViewModel
{
    public string NombreEsal { get; set; } = string.Empty;
    public bool EsAdmin { get; set; }
    public bool EsAdminPrincipal { get; set; }
    public string? Perfil { get; set; }
    public IReadOnlyList<ModuloActivo> Modulos { get; set; } = Array.Empty<ModuloActivo>();
    public int UsuariosActivos { get; set; }
    public bool PasarelaActiva { get; set; }
    public int RedesConfiguradas { get; set; }
}

public class UsuarioEsalItem
{
    public string Id { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public string? Perfil { get; set; }
    public bool Activo { get; set; }
    public bool EsUsuarioActual { get; set; }

    public string RolPerfil => $"{Rol}|{Perfil}";
}

public class UsuarioEsalFormViewModel
{
    public string? Id { get; set; }

    [Required(ErrorMessage = "Ingresa el nombre completo.")]
    [StringLength(150)]
    [Display(Name = "Nombre completo")]
    public string NombreCompleto { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa el correo.")]
    [EmailAddress(ErrorMessage = "El correo no es válido.")]
    [Display(Name = "Correo electrónico")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona el rol.")]
    [Display(Name = "Rol en la fundación")]
    public string RolPerfil { get; set; } = string.Empty;

    public static readonly (string Valor, string Texto, string Descripcion)[] Opciones =
    {
        ($"{Roles.AdministradorESAL}|{Perfiles.Principal}", "Administrador principal", "Gestiona todo: usuarios, perfil, módulos y configuración."),
        ($"{Roles.AdministradorESAL}|{Perfiles.Consulta}", "Administrador de consulta", "Consulta la información y aprueba solicitudes, sin cambiar la configuración."),
        ($"{Roles.Voluntario}|{Perfiles.Salud}", "Voluntario de salud", "Practicante con formación veterinaria: historia clínica, medicamentos y agenda."),
        ($"{Roles.Voluntario}|{Perfiles.General}", "Voluntario general", "Apoya en labores generales. No ve información clínica.")
    };

    public static string TextoDe(string rolPerfil) =>
        Opciones.FirstOrDefault(o => o.Valor == rolPerfil).Texto ?? rolPerfil;
}

public class RedesViewModel
{
    [Display(Name = "Facebook")]
    public string? Facebook { get; set; }

    [Display(Name = "Instagram")]
    public string? Instagram { get; set; }

    [Display(Name = "TikTok")]
    public string? TikTok { get; set; }
}

public class PasarelaViewModel
{
    [Required(ErrorMessage = "Ingresa la llave pública.")]
    [Display(Name = "Llave pública")]
    public string LlavePublica { get; set; } = string.Empty;

    [Display(Name = "Secreto de integridad")]
    public string? SecretoIntegridad { get; set; }

    [Display(Name = "Secreto de eventos")]
    public string? SecretoEventos { get; set; }

    public bool TieneConfiguracion { get; set; }
    public bool Activa { get; set; }
    public string? Ambiente { get; set; }
}
