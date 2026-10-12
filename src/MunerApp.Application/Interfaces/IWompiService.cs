using MunerApp.Domain.Enums;

namespace MunerApp.Application.Interfaces;

public enum ResultadoValidacionLlave
{
    Valida,
    Invalida,
    SinConexion
}

public enum EstadoTransaccionWompi
{
    Pendiente,
    Aprobada,
    Rechazada,
    Anulada,
    Error
}

/// <summary>Datos de una transacción consultada en el API de Wompi.</summary>
public record TransaccionWompi(string Id, string Referencia, long MontoCentavos, string Moneda, EstadoTransaccionWompi Estado, string? MetodoPago);

public interface IWompiService
{
    /// <summary>Firma de integridad del checkout: SHA-256 de referencia + monto en centavos + moneda + secreto de integridad (HU-043).</summary>
    string GenerarFirmaIntegridad(string referencia, long montoCentavos, string moneda, string secretoIntegridad);

    /// <summary>Consulta la transacción en Wompi. Devuelve null si no existe o no se pudo contactar a Wompi (HU-043).</summary>
    Task<TransaccionWompi?> ConsultarTransaccionAsync(string transaccionId, AmbientePasarela ambiente, CancellationToken ct = default);

    /// <summary>Consulta a Wompi si la llave pública existe (HU-045).</summary>
    Task<ResultadoValidacionLlave> ValidarLlavePublicaAsync(string llavePublica, AmbientePasarela ambiente, CancellationToken ct = default);
}
