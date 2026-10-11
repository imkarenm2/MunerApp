using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MunerApp.Web.Servicios;

namespace MunerApp.Web.Controllers;

/// <summary>
/// HU-044: URL de eventos que cada fundación configura en su panel de Wompi
/// (https://{dominio}/api/wompi/eventos/{esalId}). Es anónima y sin token antifalsificación
/// porque la llama Wompi; la autenticidad se garantiza con la firma del evento.
/// </summary>
[AllowAnonymous]
[IgnoreAntiforgeryToken]
[Route("api/wompi")]
public class WompiController : Controller
{
    private readonly ConfirmacionPagosWompi _confirmacion;

    public WompiController(ConfirmacionPagosWompi confirmacion) => _confirmacion = confirmacion;

    [HttpPost("eventos/{esalId:int}")]
    [RequestSizeLimit(64 * 1024)]
    public async Task<IActionResult> Eventos(int esalId, CancellationToken ct)
    {
        using var lector = new StreamReader(Request.Body);
        var cuerpo = await lector.ReadToEndAsync(ct);
        var respuesta = await _confirmacion.ProcesarAsync(esalId, cuerpo, ct);
        return StatusCode(respuesta.CodigoHttp);
    }

    /// <summary>La URL para configurar en Wompi, con el dominio actual.</summary>
    public static string UrlEventos(HttpRequest request, int esalId) => $"{request.Scheme}://{request.Host}/api/wompi/eventos/{esalId}";
}
