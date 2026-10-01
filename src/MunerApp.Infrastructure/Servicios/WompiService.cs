using Microsoft.Extensions.Logging;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Enums;

namespace MunerApp.Infrastructure.Servicios;

public class WompiService : IWompiService
{
    private const string UrlSandbox = "https://sandbox.wompi.co/v1/";
    private const string UrlProduccion = "https://production.wompi.co/v1/";

    private readonly HttpClient _http;
    private readonly ILogger<WompiService> _logger;

    public WompiService(HttpClient http, ILogger<WompiService> logger)
    {
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(10);
        _logger = logger;
    }

    public async Task<ResultadoValidacionLlave> ValidarLlavePublicaAsync(
        string llavePublica, AmbientePasarela ambiente, CancellationToken ct = default)
    {
        var baseUrl = ambiente == AmbientePasarela.Produccion ? UrlProduccion : UrlSandbox;
        try
        {
            using var respuesta = await _http.GetAsync($"{baseUrl}merchants/{Uri.EscapeDataString(llavePublica)}", ct);
            return respuesta.IsSuccessStatusCode ? ResultadoValidacionLlave.Valida : ResultadoValidacionLlave.Invalida;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "No se pudo contactar a Wompi para validar la llave pública.");
            return ResultadoValidacionLlave.SinConexion;
        }
    }
}
