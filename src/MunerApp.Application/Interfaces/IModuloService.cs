namespace MunerApp.Application.Interfaces;

public interface IModuloService
{
    Task<bool> EstaActivoAsync(int esalId, string codigoModulo, CancellationToken ct = default);
}
