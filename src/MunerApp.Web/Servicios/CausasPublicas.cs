using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Models.Publico;

namespace MunerApp.Web.Servicios;

/// <summary>
/// Consulta las causas de recaudación para las páginas públicas (HU-042).
/// Usa IgnoreQueryFilters porque cualquier visitante (incluido un administrador de otra fundación) las ve:
/// por eso filtra siempre de forma explícita y solo con fundaciones activas.
/// </summary>
public class CausasPublicas
{
    private readonly MunerAppDbContext _db;
    private readonly IAlmacenamientoArchivos _archivos;

    public CausasPublicas(MunerAppDbContext db, IAlmacenamientoArchivos archivos)
    {
        _db = db;
        _archivos = archivos;
    }

    /// <summary>Causas de una fundación (o de todas si <paramref name="esalId"/> es null), abiertas primero.</summary>
    public async Task<CausasPublicasViewModel> ListarAsync(int? esalId, int maxCerradas = 6)
    {
        var publicos = Causa.EstadosPublicos;
        var consulta = _db.Causas.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.Esal!.Activa && c.Esal.Slug != null && publicos.Contains(c.Estado));
        if (esalId is int id) consulta = consulta.Where(c => c.EsalId == id);

        var causas = await consulta
            .Include(c => c.Esal)
            .Include(c => c.Fotos)
            .ToListAsync();
        var recaudos = await RecaudadoAsync(causas.Select(c => c.Id).ToList());
        var hoy = DateTime.Today;

        var tarjetas = causas.Select(c => Tarjeta(c, recaudos.GetValueOrDefault(c.Id), hoy)).ToList();
        foreach (var t in tarjetas) t.MostrarFundacion = esalId is null;
        return new CausasPublicasViewModel
        {
            // Las que están por terminar primero; las pausadas después de las activas
            Abiertas = tarjetas.Where(t => !t.Cerrada).OrderBy(t => t.Pausada).ThenBy(t => t.FechaLimite).ThenBy(t => t.Id).ToList(),
            Cerradas = tarjetas.Where(t => t.Cerrada).OrderByDescending(t => t.FechaLimite).ThenByDescending(t => t.Id).Take(maxCerradas).ToList()
        };
    }

    public async Task<CausaDetalleViewModel?> ObtenerAsync(int esalId, int causaId)
    {
        var publicos = Causa.EstadosPublicos;
        var c = await _db.Causas.IgnoreQueryFilters().AsNoTracking()
            .Include(x => x.Esal).Include(x => x.Fotos)
            .FirstOrDefaultAsync(x => x.Id == causaId && x.EsalId == esalId && x.Esal!.Activa && x.Esal.Slug != null
                && publicos.Contains(x.Estado));
        if (c is null) return null;

        var recaudado = (await RecaudadoAsync(new List<int> { c.Id })).GetValueOrDefault(c.Id);
        var t = Tarjeta(c, recaudado, DateTime.Today);

        string? rendicion = null;
        if (c.RendicionDocumentoId is int docId)
        {
            var ruta = await _db.DocumentosTransparencia.IgnoreQueryFilters().AsNoTracking()
                .Where(d => d.Id == docId && d.EsalId == esalId && d.Visible)
                .Select(d => d.Ruta).FirstOrDefaultAsync();
            if (ruta is not null) rendicion = _archivos.UrlPublica(ruta);
        }

        return new CausaDetalleViewModel
        {
            Id = t.Id, Titulo = t.Titulo, FotoUrl = t.FotoUrl, Meta = t.Meta, Recaudado = t.Recaudado, Porcentaje = t.Porcentaje,
            DiasRestantes = t.DiasRestantes, FechaLimite = t.FechaLimite, Cerrada = t.Cerrada, Pausada = t.Pausada,
            NombreEsal = t.NombreEsal, SlugEsal = t.SlugEsal, LogoUrl = t.LogoUrl,
            Descripcion = c.Descripcion,
            Fotos = c.Fotos.OrderBy(f => f.Orden).ThenBy(f => f.Id).Select(f => _archivos.UrlPublica(f.Ruta)).ToList(),
            FechaPublicacion = c.FechaCreacion,
            RendicionUrl = rendicion,
            PagosEnLineaActivos = await _db.ConfigPasarelas.IgnoreQueryFilters().AnyAsync(p => p.EsalId == esalId && p.Activa)
        };
    }

    private CausaTarjeta Tarjeta(Causa c, decimal recaudado, DateTime hoy) => new()
    {
        Id = c.Id,
        Titulo = c.Titulo,
        FotoUrl = c.Fotos.OrderBy(f => f.Orden).ThenBy(f => f.Id).Select(f => _archivos.UrlPublica(f.Ruta)).FirstOrDefault(),
        Meta = c.Meta,
        Recaudado = recaudado,
        Porcentaje = Formatos.Porcentaje(recaudado, c.Meta),
        DiasRestantes = c.DiasRestantes(hoy),
        FechaLimite = c.FechaLimite,
        Cerrada = c.EstaCerrada(recaudado, hoy),
        Pausada = c.Estado == EstadoCausa.Pausada,
        NombreEsal = c.Esal!.Nombre,
        SlugEsal = c.Esal.Slug!,
        LogoUrl = c.Esal.LogoRuta is null ? null : _archivos.UrlPublica(c.Esal.LogoRuta)
    };

    private async Task<Dictionary<int, decimal>> RecaudadoAsync(List<int> causaIds)
    {
        if (causaIds.Count == 0) return new();
        return await _db.Donaciones.IgnoreQueryFilters().AsNoTracking()
            .Where(d => d.CausaId != null && causaIds.Contains(d.CausaId.Value) && d.Estado == EstadoDonacion.Confirmada)
            .GroupBy(d => d.CausaId!.Value)
            .Select(g => new { g.Key, Total = g.Sum(x => x.Valor) })
            .ToDictionaryAsync(x => x.Key, x => x.Total);
    }
}
