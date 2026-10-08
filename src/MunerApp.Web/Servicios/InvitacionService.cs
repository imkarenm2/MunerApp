using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using MunerApp.Application.Interfaces;
using MunerApp.Infrastructure.Identity;

namespace MunerApp.Web.Servicios;

public record ResultadoEnvio(bool Enviado, string Enlace);

/// <summary>Envía los correos de invitación (usuarios nuevos) y de recuperación de contraseña (HU-004).</summary>
public class InvitacionService
{
    private readonly UserManager<Usuario> _userManager;
    private readonly ICorreoService _correo;
    private readonly LinkGenerator _links;
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<InvitacionService> _logger;

    public InvitacionService(
        UserManager<Usuario> userManager,
        ICorreoService correo,
        LinkGenerator links,
        IHttpContextAccessor http,
        ILogger<InvitacionService> logger)
    {
        _userManager = userManager;
        _correo = correo;
        _links = links;
        _http = http;
        _logger = logger;
    }

    public async Task<ResultadoEnvio> EnviarInvitacionAsync(Usuario usuario, string nombreEsal)
    {
        var enlace = await GenerarEnlaceAsync(usuario, invitacion: true);
        var html = PlantillasCorreo.Invitacion(usuario.NombreCompleto, nombreEsal, enlace);
        return await EnviarAsync(usuario.Email!, "Te invitaron a MunerApp", html, enlace);
    }

    public async Task<ResultadoEnvio> EnviarRecuperacionAsync(Usuario usuario)
    {
        var enlace = await GenerarEnlaceAsync(usuario, invitacion: false);
        var html = PlantillasCorreo.Recuperacion(usuario.NombreCompleto, enlace);
        return await EnviarAsync(usuario.Email!, "Restablece tu contraseña de MunerApp", html, enlace);
    }

    private async Task<string> GenerarEnlaceAsync(Usuario usuario, bool invitacion)
    {
        var token = await _userManager.GeneratePasswordResetTokenAsync(usuario);
        var tokenCodificado = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        return _links.GetUriByAction(
            _http.HttpContext!,
            action: "RestablecerContrasena",
            controller: "Cuenta",
            values: new { area = "", email = usuario.Email, token = tokenCodificado, invitacion }) ?? string.Empty;
    }

    private async Task<ResultadoEnvio> EnviarAsync(string para, string asunto, string html, string enlace)
    {
        try
        {
            await _correo.EnviarAsync(para, asunto, html);
            return new ResultadoEnvio(true, enlace);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo enviar el correo a {Para}.", para);
            return new ResultadoEnvio(false, enlace);
        }
    }
}
