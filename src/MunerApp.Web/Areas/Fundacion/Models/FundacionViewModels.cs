using MunerApp.Web.Validacion;
using System.ComponentModel.DataAnnotations;
using MunerApp.Domain.Constantes;

namespace MunerApp.Web.Areas.Fundacion.Models;

public record ModuloActivo(string Codigo, string Nombre, bool EsConfigurable);

/// <summary>Inicio del panel: tablero con lo que pasa hoy en la fundación.</summary>
public class PanelViewModel
{
    public string NombreEsal { get; set; } = string.Empty;
    public string NombreUsuario { get; set; } = string.Empty;
    public string RolTexto { get; set; } = string.Empty;
    public DateTime Hoy { get; set; }
    public bool EsAdmin { get; set; }
    public bool EsAdminPrincipal { get; set; }
    public bool AccesoClinico { get; set; }
    public bool ModuloBeneficiarios { get; set; }
    public string? Slug { get; set; }

    /// <summary>Foto de la galería de la fundación para la cabecera; si no hay se muestra la ilustración.</summary>
    public string? FotoPortada { get; set; }

    /// <summary>Algunos gatos que están en la fundación (con foto) para la cabecera.</summary>
    public IReadOnlyList<GatoMini> Gatos { get; set; } = Array.Empty<GatoMini>();

    // ---- Indicadores ----
    public int GatosEnCasa { get; set; }
    public int EnTratamiento { get; set; }
    public int Adoptables { get; set; }
    public int EnLaFundacion { get; set; }
    public int HogaresEsteMes { get; set; }
    public int HogaresTotal { get; set; }
    public decimal DonadoEsteMes { get; set; }
    public int DonacionesEsteMes { get; set; }
    public int PadrinosActivos { get; set; }
    public decimal AporteMensualPadrinos { get; set; }

    public IReadOnlyList<TareaPanel> Tareas { get; set; } = Array.Empty<TareaPanel>();
    public IReadOnlyList<CausaPanel> Causas { get; set; } = Array.Empty<CausaPanel>();
    public IReadOnlyList<ActividadPanel> Actividad { get; set; } = Array.Empty<ActividadPanel>();
}

public record GatoMini(int Id, string Nombre);

/// <summary>Algo pendiente que el equipo debería atender. Tono: alerta, error, exito o vacío (principal).</summary>
public record TareaPanel(string Icono, string Tono, string Titulo, string Detalle, string Url);

public record CausaPanel(int Id, string Titulo, decimal Meta, decimal Recaudado, int DiasRestantes);

public record ActividadPanel(DateTime FechaUtc, string Icono, string Texto, string? Url);

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
    [Correo]
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
