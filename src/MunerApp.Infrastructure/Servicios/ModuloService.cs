using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Entities;
using MunerApp.Infrastructure.Persistence;

namespace MunerApp.Infrastructure.Servicios;

public class ModuloService : IModuloService
{
    private readonly MunerAppDbContext _db;

    public ModuloService(MunerAppDbContext db) => _db = db;

    public async Task<bool> EstaActivoAsync(int esalId, string codigoModulo, CancellationToken ct = default)
    {
        var modulo = await _db.Modulos.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Codigo == codigoModulo, ct);

        if (modulo is null) return false;
        if (!modulo.EsConfigurable) return true; // los módulos generales siempre están activos

        return await _db.EsalModulos.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(em => em.EsalId == esalId && em.ModuloId == modulo.Id && em.Activo, ct);
    }

    public async Task<IReadOnlyList<Modulo>> ObtenerActivosAsync(int esalId, CancellationToken ct = default)
    {
        var configurablesActivos = await _db.EsalModulos.IgnoreQueryFilters().AsNoTracking()
            .Where(em => em.EsalId == esalId && em.Activo)
            .Select(em => em.ModuloId)
            .ToListAsync(ct);

        return await _db.Modulos.AsNoTracking()
            .Where(m => !m.EsConfigurable || configurablesActivos.Contains(m.Id))
            .OrderBy(m => m.Id)
            .ToListAsync(ct);
    }
}
