using Microsoft.EntityFrameworkCore;
using MunerApp.Infrastructure.Persistence;

namespace MunerApp.Web.Servicios;

/// <summary>Primera foto de cada fundación, para usarla como portada en sus tarjetas del directorio y del inicio.</summary>
public static class PortadasEsal
{
    /// <summary>Ruta de la primera foto por fundación (las que no tienen fotos no aparecen). Es información pública.</summary>
    public static async Task<Dictionary<int, string>> ObtenerAsync(MunerAppDbContext db, IReadOnlyCollection<int> esalIds)
    {
        var fotos = await db.FotosEsal.IgnoreQueryFilters().AsNoTracking()
            .Where(f => esalIds.Contains(f.EsalId))
            .OrderBy(f => f.Orden).ThenBy(f => f.Id)
            .Select(f => new { f.EsalId, f.Ruta })
            .ToListAsync();
        return fotos.GroupBy(f => f.EsalId).ToDictionary(g => g.Key, g => g.First().Ruta);
    }
}
