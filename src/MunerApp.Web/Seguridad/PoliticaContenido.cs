namespace MunerApp.Web.Seguridad;

/// <summary>
/// Content-Security-Policy (seguridad): de dónde puede cargar la página scripts, estilos, letras, imágenes y a dónde
/// puede enviar formularios. Se envía en modo "solo reporte" (Program.cs) mientras se confirma que nada la viola.
/// 'unsafe-inline' en estilos es necesario por los style="" de las vistas (por ejemplo, el ancho de las barras de progreso)
/// y en scripts por un bloque en línea de la agenda; al quitarlos se puede endurecer.
/// </summary>
public static class PoliticaContenido
{
    public const string Valor =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com https://cdn.jsdelivr.net; " +
        "img-src 'self' data: blob:; " +
        "media-src 'self'; " +
        "connect-src 'self'; " +
        "frame-ancestors 'self'; " +
        "form-action 'self' https://checkout.wompi.co https://accounts.google.com; " +
        "base-uri 'self'; " +
        "object-src 'none'";
}
