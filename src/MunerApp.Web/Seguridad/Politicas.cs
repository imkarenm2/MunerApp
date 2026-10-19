using System.Security.Claims;
using MunerApp.Application.Seguridad;
using MunerApp.Domain.Constantes;

namespace MunerApp.Web.Seguridad;

/// <summary>
/// Permisos por rol. La tabla completa de quién puede hacer qué está en el README (sección "Roles y permisos").
/// </summary>
public static class Politicas
{
    /// <summary>Administrador de ESAL con perfil Principal: gestiona usuarios, configuración y contenidos (HU-008).</summary>
    public const string AdminEsalPrincipal = "AdminEsalPrincipal";

    /// <summary>
    /// Ver la historia clínica (HU-019): administradores de la ESAL (principal y de consulta) y voluntarios de salud.
    /// El voluntario general no la ve.
    /// </summary>
    public const string AccesoClinico = "AccesoClinico";

    /// <summary>
    /// Registrar eventos clínicos (HU-019): administrador principal y voluntarios de salud.
    /// El administrador de consulta solo la ve.
    /// </summary>
    public const string RegistroClinico = "RegistroClinico";

    public static bool EsAdminPrincipal(ClaimsPrincipal usuario) =>
        usuario.IsInRole(Roles.AdministradorESAL) && usuario.HasClaim(MunerAppClaims.Perfil, Perfiles.Principal);

    public static bool EsVoluntarioSalud(ClaimsPrincipal usuario) =>
        usuario.IsInRole(Roles.Voluntario) && usuario.HasClaim(MunerAppClaims.Perfil, Perfiles.Salud);

    public static bool TieneAccesoClinico(ClaimsPrincipal usuario) =>
        usuario.IsInRole(Roles.AdministradorESAL) || EsVoluntarioSalud(usuario);

    public static bool PuedeRegistrarClinica(ClaimsPrincipal usuario) =>
        EsAdminPrincipal(usuario) || EsVoluntarioSalud(usuario);

    /// <summary>
    /// Donar, apadrinar, reportar aportes y postularse como voluntario: solo cuentas de donante.
    /// Las cuentas de una fundación (administradores y voluntarios) y el superadministrador no pueden,
    /// para evitar que una fundación se reporte y se confirme donaciones a sí misma.
    /// </summary>
    public static bool PuedeApoyar(ClaimsPrincipal usuario) =>
        usuario.Identity?.IsAuthenticated == true
        && !usuario.IsInRole(Roles.AdministradorESAL)
        && !usuario.IsInRole(Roles.Voluntario)
        && !usuario.IsInRole(Roles.SuperAdministrador);

    /// <summary>Cuenta de una fundación o de la plataforma (no es donante).</summary>
    public static bool EsCuentaInstitucional(ClaimsPrincipal usuario) =>
        usuario.Identity?.IsAuthenticated == true && !PuedeApoyar(usuario);

    public const string MensajeCuentaInstitucional =
        "Las cuentas de una fundación o de la plataforma no pueden donar, apadrinar ni postularse. Si quieres apoyar a título personal, crea una cuenta con tu correo personal.";
}
