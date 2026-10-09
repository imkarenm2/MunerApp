using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Constantes;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Models.Publico;

namespace MunerApp.Web.Servicios;

/// <summary>
/// Arma "El periódico de MunerApp" (HU-028): lo que publican las fundaciones en su boletín más las buenas noticias
/// que se generan solas con la actividad de la plataforma (adopciones, causas que llegan a la meta, fundaciones
/// nuevas y productos nuevos en las tiendas). Es público: usa IgnoreQueryFilters y solo toma fundaciones activas.
/// Nunca muestra datos personales: de los donantes solo se publican totales, y de las adopciones el nombre del peludo.
/// </summary>
public class PeriodicoMunerApp
{
    private const int DiasBuenasNoticias = 90;
    private const int MaxBuenasNoticias = 8;

    private readonly MunerAppDbContext _db;
    private readonly IAlmacenamientoArchivos _archivos;

    public PeriodicoMunerApp(MunerAppDbContext db, IAlmacenamientoArchivos archivos)
    {
        _db = db;
        _archivos = archivos;
    }

    public async Task<PeriodicoViewModel> ArmarAsync(int recientes = 4)
    {
        var ahora = DateTime.UtcNow;
        var publicadas = _db.Publicaciones.IgnoreQueryFilters().AsNoTracking().Include(p => p.Esal)
            .Where(p => p.Estado == EstadoPublicacion.Publicada && p.Esal!.Activa && p.Esal.Slug != null);

        var proximos = await publicadas
            .Where(p => p.Categoria == CategoriaPublicacion.Evento && p.FechaEvento >= ahora)
            .OrderBy(p => p.FechaEvento).Take(4).ToListAsync();
        var ultimas = await publicadas
            .OrderByDescending(p => p.FechaPublicacion).Take(recientes + 6).ToListAsync();

        // Titular: el próximo evento (si es en los próximos 60 días) o, si no, la publicación más reciente
        var titular = proximos.FirstOrDefault(p => p.FechaEvento <= ahora.AddDays(60)) ?? ultimas.FirstOrDefault();

        return new PeriodicoViewModel
        {
            Titular = titular is null ? null : Tarjeta(titular),
            ProximosEventos = proximos.Where(p => p.Id != titular?.Id).Take(3).Select(Tarjeta).ToList(),
            // Los eventos futuros ya salen en "Próximos eventos": aquí van las demás publicaciones
            Recientes = ultimas.Where(p => p.Id != titular?.Id && !(p.FechaEvento >= ahora))
                .Take(recientes).Select(Tarjeta).ToList(),
            BuenasNoticias = await BuenasNoticiasAsync(ahora),
            Fundaciones = await _db.Esales.CountAsync(e => e.Activa && e.Slug != null),
            Adopciones = await _db.Beneficiarios.IgnoreQueryFilters().CountAsync(b => b.Estado == EstadoBeneficiario.Adoptado && b.Esal!.Activa),
            CausasCumplidas = (await RecaudoCausasAsync()).Count(c => c.Recaudado >= c.Meta),
            TotalDonado = await _db.Donaciones.IgnoreQueryFilters()
                .Where(d => d.Estado == EstadoDonacion.Confirmada && d.Esal!.Activa).SumAsync(d => (decimal?)d.Valor) ?? 0
        };
    }

    private async Task<List<BuenaNoticia>> BuenasNoticiasAsync(DateTime ahora)
    {
        var desde = ahora.AddDays(-DiasBuenasNoticias);
        var noticias = new List<BuenaNoticia>();

        // Adopciones: el peludo encontró familia (de la familia no se publica nada)
        var adopciones = await _db.HistorialEstadosBeneficiario.IgnoreQueryFilters().AsNoTracking()
            .Where(h => h.Estado == EstadoBeneficiario.Adoptado && h.Fecha >= desde && h.Beneficiario!.Esal!.Activa && h.Beneficiario.Esal.Slug != null)
            .OrderByDescending(h => h.Fecha).Take(MaxBuenasNoticias)
            .Select(h => new { h.Fecha, h.Beneficiario!.Nombre, Esal = h.Beneficiario.Esal!.Nombre, h.Beneficiario.Esal.Slug })
            .ToListAsync();
        noticias.AddRange(adopciones.Select(a => new BuenaNoticia("bi-house-heart-fill", "exito",
            $"¡{a.Nombre} encontró una familia!", $"Fue adoptado en {a.Esal}.", $"/fundaciones/{a.Slug}", a.Fecha)));

        // Causas que llegaron a su meta
        foreach (var c in (await RecaudoCausasAsync()).Where(c => c.Recaudado >= c.Meta && c.UltimaDonacion >= desde))
            noticias.Add(new BuenaNoticia("bi-bullseye", "acento",
                $"La causa «{c.Titulo}» llegó a su meta", $"{c.Esal} reunió {Formatos.Pesos(c.Recaudado)}. ¡Gracias a cada donante!",
                $"/fundaciones/{c.Slug}/causas/{c.Id}", c.UltimaDonacion!.Value));

        // Fundaciones nuevas
        var nuevas = await _db.Esales.AsNoTracking()
            .Where(e => e.Activa && e.Slug != null && e.FechaRegistro >= desde)
            .OrderByDescending(e => e.FechaRegistro).Take(MaxBuenasNoticias)
            .Select(e => new { e.Nombre, e.Slug, e.Ciudad, e.FechaRegistro }).ToListAsync();
        noticias.AddRange(nuevas.Select(e => new BuenaNoticia("bi-stars", "",
            $"{e.Nombre} se unió a MunerApp", e.Ciudad is null ? "Ya puedes conocerla y apoyarla." : $"Desde {e.Ciudad}. Ya puedes conocerla y apoyarla.",
            $"/fundaciones/{e.Slug}", e.FechaRegistro)));

        // Productos nuevos en las tiendas (solo fundaciones con el módulo Tienda activo)
        var productos = await _db.Productos.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.Estado == EstadoProducto.Disponible && p.FechaCreacion >= desde && p.Esal!.Activa && p.Esal.Slug != null
                && _db.EsalModulos.IgnoreQueryFilters().Any(m => m.EsalId == p.EsalId && m.Activo && m.Modulo!.Codigo == CodigosModulo.Tienda))
            .OrderByDescending(p => p.FechaCreacion).Take(3)
            .Select(p => new { p.Id, p.Nombre, p.FechaCreacion, Esal = p.Esal!.Nombre, p.Esal.Slug }).ToListAsync();
        noticias.AddRange(productos.Select(p => new BuenaNoticia("bi-bag-heart-fill", "alerta",
            $"Nuevo en la tienda: {p.Nombre}", $"Lo vende {p.Esal}. Cada compra la ayuda.", $"/fundaciones/{p.Slug}/tienda/{p.Id}", p.FechaCreacion)));

        return noticias.OrderByDescending(n => n.Fecha).Take(MaxBuenasNoticias).ToList();
    }

    private record RecaudoCausa(int Id, string Titulo, decimal Meta, decimal Recaudado, DateTime? UltimaDonacion, string Esal, string Slug);

    /// <summary>Lo recaudado por cada causa de fundaciones activas (suma de sus donaciones confirmadas).</summary>
    private Task<List<RecaudoCausa>> RecaudoCausasAsync()
        => _db.Causas.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.Esal!.Activa && c.Esal.Slug != null)
            .Select(c => new RecaudoCausa(c.Id, c.Titulo, c.Meta,
                _db.Donaciones.IgnoreQueryFilters().Where(d => d.CausaId == c.Id && d.Estado == EstadoDonacion.Confirmada).Sum(d => (decimal?)d.Valor) ?? 0,
                _db.Donaciones.IgnoreQueryFilters().Where(d => d.CausaId == c.Id && d.Estado == EstadoDonacion.Confirmada).Max(d => d.FechaRevision),
                c.Esal!.Nombre, c.Esal.Slug!))
            .ToListAsync();

    private PublicacionTarjeta Tarjeta(Publicacion p)
    {
        var t = TarjetasBoletin.Desde(p, _archivos);
        t.MostrarFundacion = true;
        return t;
    }
}
