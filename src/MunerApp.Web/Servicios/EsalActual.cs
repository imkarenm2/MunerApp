using System.Security.Claims;
using MunerApp.Application.Interfaces;
using MunerApp.Application.Seguridad;
using MunerApp.Domain.Constantes;

namespace MunerApp.Web.Servicios;

public class EsalActual : IEsalActual
{
    private readonly IHttpContextAccessor _http;

    public EsalActual(IHttpContextAccessor http) => _http = http;

    private ClaimsPrincipal? Usuario => _http.HttpContext?.User;

    public bool EstaAutenticado => Usuario?.Identity?.IsAuthenticated ?? false;

    public bool EsSuperAdmin => Usuario?.IsInRole(Roles.SuperAdministrador) ?? false;

    public int? EsalId =>
        int.TryParse(Usuario?.FindFirst(MunerAppClaims.EsalId)?.Value, out var id) ? id : null;
}
