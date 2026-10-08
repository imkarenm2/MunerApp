using System.Security.Claims;
using MunerApp.Application.Seguridad;
using MunerApp.Domain.Constantes;

namespace MunerApp.Web.Seguridad;

public static class Politicas
{
    /// <summary>Administrador de ESAL con perfil Principal: gestiona usuarios y configuración (HU-008).</summary>
    public const string AdminEsalPrincipal = "AdminEsalPrincipal";

    /// <summary>
    /// Información clínica (HU-019): administradores de la ESAL y voluntarios de salud (practicantes).
    /// El voluntario general no la ve ni la registra.
    /// </summary>
    public const string AccesoClinico = "AccesoClinico";

    public static bool TieneAccesoClinico(ClaimsPrincipal usuario) =>
        usuario.IsInRole(Roles.AdministradorESAL)
        || (usuario.IsInRole(Roles.Voluntario) && usuario.HasClaim(MunerAppClaims.Perfil, Perfiles.Salud));
}
