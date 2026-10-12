using System.Text.Json;
using MunerApp.Infrastructure.Servicios;

namespace MunerApp.Tests;

/// <summary>
/// HU-044: validación de la firma de los eventos de Wompi
/// (https://docs.wompi.co/docs/colombia/eventos/).
/// </summary>
public class WompiFirmaEventosTests
{
    // Valores del ejemplo de la documentación de Wompi
    private const string Secreto = "prod_events_OcHnIzeBl5socpwByQ4hA52Em3USQ93Z";
    private const string TransaccionId = "1234-1610641025-49201";
    private const long Timestamp = 1530291411;

    /// <summary>Evento como lo envía Wompi, firmado con <paramref name="secretoFirma"/>.</summary>
    private static JsonElement Evento(string estado = "APPROVED", long centavos = 4490000, string secretoFirma = Secreto,
        string[]? propiedades = null, Func<string, string>? alterarChecksum = null)
    {
        propiedades ??= new[] { "transaction.id", "transaction.status", "transaction.amount_in_cents" };
        var valores = new Dictionary<string, string>
        {
            ["transaction.id"] = TransaccionId,
            ["transaction.status"] = estado,
            ["transaction.amount_in_cents"] = centavos.ToString()
        };
        var checksum = WompiFirmaEventos.Calcular(propiedades.Select(p => valores[p]), Timestamp.ToString(), secretoFirma);
        if (alterarChecksum is not null) checksum = alterarChecksum(checksum);

        var json = JsonSerializer.Serialize(new
        {
            @event = "transaction.updated",
            data = new
            {
                transaction = new
                {
                    id = TransaccionId,
                    amount_in_cents = centavos,
                    reference = "DON-WOMPI-000123",
                    currency = "COP",
                    payment_method_type = "NEQUI",
                    status = estado
                }
            },
            environment = "test",
            signature = new { properties = propiedades, checksum },
            timestamp = Timestamp,
            sent_at = "2026-10-08T21:00:00.000Z"
        });
        return JsonDocument.Parse(json).RootElement;
    }

    [Fact]
    public void Calcular_concatena_valores_timestamp_y_secreto_en_ese_orden()
    {
        // SHA256 de la cadena que muestra la documentación de Wompi:
        // "1234-1610641025-49201APPROVED44900001530291411prod_events_OcHnIzeBl5socpwByQ4hA52Em3USQ93Z"
        var checksum = WompiFirmaEventos.Calcular(new[] { TransaccionId, "APPROVED", "4490000" }, "1530291411", Secreto);

        Assert.Equal("5A18EC5E8FDB7DF463E9F94774CBA8F583BA21BD04A09CEFF2EA68A4BC0AEFBE", checksum);
    }

    [Fact]
    public void Evento_firmado_con_el_secreto_de_la_fundacion_es_valido()
    {
        Assert.True(WompiFirmaEventos.EsValida(Evento(), Secreto));
    }

    [Fact]
    public void Escenario2_firma_con_otro_secreto_es_invalida()
    {
        var evento = Evento(secretoFirma: "test_events_de_otra_fundacion");

        Assert.False(WompiFirmaEventos.EsValida(evento, Secreto));
    }

    [Fact]
    public void Escenario2_evento_alterado_despues_de_firmado_es_invalido()
    {
        // Se firmó como DECLINED pero alguien cambió el estado a APPROVED
        var firmado = Evento(estado: "DECLINED");
        var json = firmado.GetRawText().Replace("\"status\":\"DECLINED\"", "\"status\":\"APPROVED\"");

        Assert.False(WompiFirmaEventos.EsValida(JsonDocument.Parse(json).RootElement, Secreto));
    }

    [Fact]
    public void Escenario2_monto_alterado_es_invalido()
    {
        var json = Evento().GetRawText().Replace("4490000", "100");

        Assert.False(WompiFirmaEventos.EsValida(JsonDocument.Parse(json).RootElement, Secreto));
    }

    [Fact]
    public void Checksum_en_minusculas_tambien_es_valido()
    {
        Assert.True(WompiFirmaEventos.EsValida(Evento(alterarChecksum: c => c.ToLowerInvariant()), Secreto));
    }

    [Fact]
    public void Usa_las_propiedades_y_el_orden_que_indica_el_evento()
    {
        var evento = Evento(propiedades: new[] { "transaction.status", "transaction.id" });

        Assert.True(WompiFirmaEventos.EsValida(evento, Secreto));
    }

    [Fact]
    public void Propiedad_que_no_existe_en_los_datos_es_invalida()
    {
        var json = Evento().GetRawText().Replace("\"transaction.status\"", "\"transaction.no_existe\"");

        Assert.False(WompiFirmaEventos.EsValida(JsonDocument.Parse(json).RootElement, Secreto));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"event\":\"transaction.updated\",\"data\":{}}")]
    [InlineData("{\"signature\":{\"checksum\":\"ABC\"},\"timestamp\":1,\"data\":{}}")]
    [InlineData("[]")]
    public void Evento_sin_firma_completa_es_invalido(string json)
    {
        Assert.False(WompiFirmaEventos.EsValida(JsonDocument.Parse(json).RootElement, Secreto));
    }

    [Fact]
    public void Sin_secreto_configurado_nada_es_valido()
    {
        Assert.False(WompiFirmaEventos.EsValida(Evento(), ""));
    }
}
