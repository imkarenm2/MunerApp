using MunerApp.Domain.Enums;

namespace MunerApp.Application.Interfaces;

public enum ResultadoValidacionLlave
{
    Valida,
    Invalida,
    SinConexion
}

public interface IWompiService
{
    /// <summary>Consulta a Wompi si la llave pública existe (HU-045).</summary>
    Task<ResultadoValidacionLlave> ValidarLlavePublicaAsync(string llavePublica, AmbientePasarela ambiente, CancellationToken ct = default);
}
