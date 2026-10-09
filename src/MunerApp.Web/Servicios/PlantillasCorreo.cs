using System.Net;

namespace MunerApp.Web.Servicios;

/// <summary>Correos HTML simples con la identidad de MunerApp.</summary>
public static class PlantillasCorreo
{
    public static string Invitacion(string nombre, string nombreEsal, string enlace) => Base(
        $"¡Hola, {Html(nombre)}!",
        $"Te agregaron como parte del equipo de <strong>{Html(nombreEsal)}</strong> en MunerApp. " +
        "Crea tu contraseña para empezar. También puedes entrar con tu cuenta de Google si usas este mismo correo.",
        "Crear mi contraseña",
        enlace,
        "Este enlace vence en 1 hora. Si vence, usa la opción \"¿Olvidaste tu contraseña?\" en la página de inicio de sesión.");

    public static string Recuperacion(string nombre, string enlace) => Base(
        $"Hola, {Html(nombre)}",
        "Recibimos una solicitud para restablecer tu contraseña de MunerApp.",
        "Restablecer contraseña",
        enlace,
        "Este enlace vence en 1 hora y solo se puede usar una vez. Si no pediste este cambio, ignora este correo.");

    public static string Confirmacion(string nombre, string enlace) => Base(
        $"¡Bienvenido a MunerApp, {Html(nombre)}!",
        "Solo falta un paso: confirma que este correo es tuyo para activar tu cuenta.",
        "Confirmar mi correo",
        enlace,
        "Este enlace vence en 1 hora. Si vence, intenta iniciar sesión y te ofreceremos enviarte uno nuevo. Si no creaste esta cuenta, ignora este correo.");

    /// <summary>Aviso general (donación confirmada, solicitud de una fundación...). El texto se codifica.</summary>
    public static string Aviso(string titulo, string mensaje, string textoBoton, string enlace, string nota = "Recibes este correo porque tienes una cuenta en MunerApp.")
        => Base(Html(titulo), Html(mensaje), textoBoton, enlace, nota);

    private static string Html(string texto) => WebUtility.HtmlEncode(texto);

    private static string Base(string titulo, string cuerpo, string textoBoton, string enlace, string nota) => $"""
        <div style="background:#FAF8F5;padding:32px 16px;font-family:Arial,Helvetica,sans-serif;color:#1F2A2E">
          <div style="max-width:520px;margin:0 auto;background:#fff;border:1px solid #E4E0DA;border-radius:14px;padding:32px">
            <div style="font-size:22px;font-weight:800;color:#0F5257;margin-bottom:24px">MunerApp</div>
            <h1 style="font-size:20px;margin:0 0 12px">{titulo}</h1>
            <p style="font-size:15px;line-height:1.6;margin:0 0 24px">{cuerpo}</p>
            <a href="{enlace}" style="display:inline-block;background:#0F5257;color:#fff;text-decoration:none;font-weight:bold;padding:12px 22px;border-radius:10px">{textoBoton}</a>
            <p style="font-size:13px;color:#5B6770;line-height:1.5;margin:24px 0 0">{nota}</p>
          </div>
        </div>
        """;
}
