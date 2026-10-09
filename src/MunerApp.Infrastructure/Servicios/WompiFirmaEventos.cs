using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MunerApp.Infrastructure.Servicios;

/// <summary>
/// Firma de los avisos (eventos) de Wompi (HU-044), según https://docs.wompi.co/docs/colombia/eventos/:
/// SHA256 de los valores de <c>signature.properties</c> (tomados de <c>data</c>, en ese orden),
/// seguidos del <c>timestamp</c> y del secreto de eventos de la fundación, sin separadores.
/// La lista de propiedades no se fija en el código porque Wompi puede cambiarla.
/// </summary>
public static class WompiFirmaEventos
{
    /// <summary>Checksum en hexadecimal en mayúsculas.</summary>
    public static string Calcular(IEnumerable<string> valores, string timestamp, string secretoEventos)
    {
        var cadena = string.Concat(valores) + timestamp + secretoEventos;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cadena)));
    }

    /// <summary>True si el checksum del evento coincide con el calculado con el secreto de eventos.</summary>
    public static bool EsValida(JsonElement evento, string secretoEventos)
    {
        if (string.IsNullOrEmpty(secretoEventos)
            || evento.ValueKind != JsonValueKind.Object
            || !evento.TryGetProperty("signature", out var firma) || firma.ValueKind != JsonValueKind.Object
            || !firma.TryGetProperty("checksum", out var checksum) || checksum.ValueKind != JsonValueKind.String
            || !firma.TryGetProperty("properties", out var propiedades) || propiedades.ValueKind != JsonValueKind.Array
            || !evento.TryGetProperty("timestamp", out var timestamp)
            || !evento.TryGetProperty("data", out var data))
            return false;

        var valores = new List<string>();
        foreach (var propiedad in propiedades.EnumerateArray())
        {
            if (propiedad.ValueKind != JsonValueKind.String || ValorDe(data, propiedad.GetString()!) is not { } valor)
                return false;
            valores.Add(valor);
        }
        if (valores.Count == 0 || Texto(timestamp) is not { } ts) return false;

        var esperado = Encoding.ASCII.GetBytes(Calcular(valores, ts, secretoEventos));
        var recibido = Encoding.ASCII.GetBytes(checksum.GetString()!.Trim().ToUpperInvariant());
        return CryptographicOperations.FixedTimeEquals(esperado, recibido);
    }

    /// <summary>Valor de una ruta como "transaction.amount_in_cents" dentro de <c>data</c>, o null si no existe.</summary>
    private static string? ValorDe(JsonElement data, string ruta)
    {
        var actual = data;
        foreach (var parte in ruta.Split('.'))
        {
            if (actual.ValueKind != JsonValueKind.Object || !actual.TryGetProperty(parte, out actual))
                return null;
        }
        return Texto(actual);
    }

    /// <summary>El texto tal como lo concatena Wompi: cadenas sin comillas y números como vienen.</summary>
    private static string? Texto(JsonElement valor) => valor.ValueKind switch
    {
        JsonValueKind.String => valor.GetString(),
        JsonValueKind.Number => valor.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => null
    };
}
