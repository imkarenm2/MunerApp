using MunerApp.Domain.Entities;

namespace MunerApp.Application.Interfaces;

public interface IModuloService
{
    Task<bool> EstaActivoAsync(int esalId, string codigoModulo, CancellationToken ct = default);

    /// <summary>Módulos generales + configurables activos de la ESAL (HU-007).</summary>
    Task<IReadOnlyList<Modulo>> ObtenerActivosAsync(int esalId, CancellationToken ct = default);
}
