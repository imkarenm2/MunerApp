using System.Security.Claims;
using MunerApp.Application.Interfaces;
using MunerApp.Application.Seguridad;

namespace MunerApp.Web.Servicios;

/// <summary>La persona con sesión en la petición actual (para el registro de auditoría).</summary>
public class UsuarioActual : IUsuarioActual
{
    private readonly IHttpContextAccessor _http;

    public UsuarioActual(IHttpContextAccessor http) => _http = http;

    private ClaimsPrincipal? Usuario => _http.HttpContext?.User;

    public string? Id => Usuario?.Identity?.IsAuthenticated == true ? Usuario.FindFirstValue(ClaimTypes.NameIdentifier) : null;

    public string? Nombre => Usuario?.FindFirstValue(MunerAppClaims.NombreCompleto) ?? Usuario?.Identity?.Name;
}
