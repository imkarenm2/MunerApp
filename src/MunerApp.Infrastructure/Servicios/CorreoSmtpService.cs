using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using MunerApp.Application.Interfaces;

namespace MunerApp.Infrastructure.Servicios;

/// <summary>Envío de correos por Gmail SMTP con contraseña de aplicación (HU-004).</summary>
public class CorreoSmtpService : ICorreoService
{
    private readonly IConfiguration _config;

    public CorreoSmtpService(IConfiguration config) => _config = config;

    public async Task EnviarAsync(string para, string asunto, string cuerpoHtml)
    {
        var s = _config.GetSection("Correo");
        var usuario = s["Usuario"] ?? throw new InvalidOperationException("Falta Correo:Usuario en la configuración.");
        var remitente = string.IsNullOrWhiteSpace(s["Remitente"]) ? usuario : s["Remitente"]!;

        using var cliente = new SmtpClient(s["Host"] ?? "smtp.gmail.com", int.Parse(s["Puerto"] ?? "587"))
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(usuario, s["Contrasena"])
        };
        using var mensaje = new MailMessage(remitente, para, asunto, cuerpoHtml) { IsBodyHtml = true };
        await cliente.SendMailAsync(mensaje);
    }
}
