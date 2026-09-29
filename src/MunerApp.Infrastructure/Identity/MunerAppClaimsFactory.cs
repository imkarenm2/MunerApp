using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MunerApp.Application.Seguridad;

namespace MunerApp.Infrastructure.Identity;

/// <summary>Agrega a la cookie de sesión la ESAL, el perfil y el nombre del usuario.</summary>
public class MunerAppClaimsFactory : UserClaimsPrincipalFactory<Usuario, IdentityRole>
{
    public MunerAppClaimsFactory(
        UserManager<Usuario> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(Usuario user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(MunerAppClaims.NombreCompleto, user.NombreCompleto));

        if (user.EsalId.HasValue)
            identity.AddClaim(new Claim(MunerAppClaims.EsalId, user.EsalId.Value.ToString()));

        if (!string.IsNullOrWhiteSpace(user.Perfil))
            identity.AddClaim(new Claim(MunerAppClaims.Perfil, user.Perfil));

        return identity;
    }
}
