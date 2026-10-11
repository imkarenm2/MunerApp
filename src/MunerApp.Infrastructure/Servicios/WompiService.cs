using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

    public string GenerarFirmaIntegridad(string referencia, long montoCentavos, string moneda, string secretoIntegridad)
    {
        var cadena = $"{referencia}{montoCentavos}{moneda}{secretoIntegridad}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cadena))).ToLowerInvariant();
    }

    public async Task<TransaccionWompi?> ConsultarTransaccionAsync(
        string transaccionId, AmbientePasarela ambiente, CancellationToken ct = default)
    {
        var baseUrl = ambiente == AmbientePasarela.Produccion ? UrlProduccion : UrlSandbox;
        try
        {
            using var respuesta = await _http.GetAsync($"{baseUrl}transactions/{Uri.EscapeDataString(transaccionId)}", ct);
            if (!respuesta.IsSuccessStatusCode) return null;

            using var json = await JsonDocument.ParseAsync(await respuesta.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var d = json.RootElement.GetProperty("data");
            var estado = d.GetProperty("status").GetString() switch
            {
                "APPROVED" => EstadoTransaccionWompi.Aprobada,
                "DECLINED" => EstadoTransaccionWompi.Rechazada,
                "VOIDED" => EstadoTransaccionWompi.Anulada,
                "ERROR" => EstadoTransaccionWompi.Error,
                _ => EstadoTransaccionWompi.Pendiente
            };
            return new TransaccionWompi(
                d.GetProperty("id").GetString() ?? transaccionId,
                d.GetProperty("reference").GetString() ?? string.Empty,
                d.GetProperty("amount_in_cents").GetInt64(),
                d.GetProperty("currency").GetString() ?? "COP",
                estado,
                d.TryGetProperty("payment_method_type", out var m) ? m.GetString() : null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
        {
            _logger.LogWarning(ex, "No se pudo consultar la transacción {Id} en Wompi.", transaccionId);
            return null;
        }
    }
}
