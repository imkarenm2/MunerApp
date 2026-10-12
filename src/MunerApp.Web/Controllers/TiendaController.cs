using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Constantes;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Models.Publico;

namespace MunerApp.Web.Controllers;

/// <summary>
/// HU-024: el visitante ve el catálogo de productos en tarjetas, sin iniciar sesión.
/// Solo muestra productos visibles (disponibles o agotados) de fundaciones activas con el módulo Tienda.
/// Usa IgnoreQueryFilters porque cualquier visitante los ve: por eso filtra siempre de forma explícita.
/// </summary>
public class TiendaController : Controller
{
    private const int MaxBusqueda = 60;

    private readonly MunerAppDbContext _db;
    private readonly IAlmacenamientoArchivos _archivos;
    private readonly IModuloService _modulos;

    public TiendaController(MunerAppDbContext db, IAlmacenamientoArchivos archivos, IModuloService modulos)
    {
        _db = db;
        _archivos = archivos;
        _modulos = modulos;
    }

    // ---------- Tienda general: productos de todas las fundaciones ----------

    [HttpGet("tienda")]
    public async Task<IActionResult> Todas(string? categoria, string? q)
    {
        var esalIds = await EsalesConTiendaAsync();
        var modelo = await CatalogoAsync(esalIds, categoria, q);
        foreach (var p in modelo.Productos) p.MostrarFundacion = true;
        return View(modelo);
    }

    // ---------- Tienda de una fundación ----------

    [HttpGet("fundaciones/{slug}/tienda")]
    public async Task<IActionResult> Index(string slug, string? categoria, string? q)
    {
        var esal = await BuscarEsalAsync(slug);
        if (esal is null) return NoDisponible();

        var modelo = await CatalogoAsync(new List<int> { esal.Id }, categoria, q);
        modelo.Slug = esal.Slug;
        modelo.NombreEsal = esal.Nombre;
        modelo.LogoUrl = esal.LogoRuta is null ? null : _archivos.UrlPublica(esal.LogoRuta);
        return View(modelo);
    }

    // ---------- Detalle del producto ----------

    [HttpGet("fundaciones/{slug}/tienda/{id:int}")]
    public async Task<IActionResult> Producto(string slug, int id)
    {
        var esal = await BuscarEsalAsync(slug);
        if (esal is null) return NoDisponible();

        var p = await _db.Productos.IgnoreQueryFilters().AsNoTracking().Include(x => x.Fotos)
            .FirstOrDefaultAsync(x => x.Id == id && x.EsalId == esal.Id && x.Estado != EstadoProducto.Oculto);
        if (p is null) return NoDisponible();

        var relacionados = await _db.Productos.IgnoreQueryFilters().AsNoTracking().Include(x => x.Fotos)
            .Where(x => x.EsalId == esal.Id && x.Id != id && x.Estado == EstadoProducto.Disponible)
            .OrderByDescending(x => x.Categoria == p.Categoria).ThenByDescending(x => x.FechaCreacion)
            .Take(4).ToListAsync();

        return View(new ProductoDetalleViewModel
        {
            Id = p.Id,
            Nombre = p.Nombre,
            Descripcion = p.Descripcion,
            Precio = p.Precio,
            Categoria = p.Categoria,
            Estado = p.Estado,
            Fotos = p.Fotos.OrderBy(f => f.Orden).ThenBy(f => f.Id).Select(f => _archivos.UrlPublica(f.Ruta)).ToList(),
            SlugEsal = esal.Slug!,
            NombreEsal = esal.Nombre,
            LogoUrl = esal.LogoRuta is null ? null : _archivos.UrlPublica(esal.LogoRuta),
            Relacionados = relacionados.Select(r => Tarjeta(r, esal)).ToList()
        });
    }

    // ---------- Apoyo ----------

    private async Task<CatalogoViewModel> CatalogoAsync(List<int> esalIds, string? categoria, string? q)
    {
        var visibles = _db.Productos.IgnoreQueryFilters().AsNoTracking()
            .Where(p => esalIds.Contains(p.EsalId) && p.Estado != EstadoProducto.Oculto);

        var categorias = await visibles.Where(p => p.Categoria != null)
            .Select(p => p.Categoria!).Distinct().OrderBy(c => c).ToListAsync();

        var consulta = visibles;
        categoria = string.IsNullOrWhiteSpace(categoria) ? null : categoria.Trim();
        if (categoria is not null) consulta = consulta.Where(p => p.Categoria == categoria);

        q = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        if (q is not null)
        {
            if (q.Length > MaxBusqueda) q = q[..MaxBusqueda];
            consulta = consulta.Where(p => p.Nombre.Contains(q) || p.Descripcion.Contains(q));
        }

        var productos = await consulta
            .Include(p => p.Fotos).Include(p => p.Esal)
            // Los disponibles primero; los agotados al final
            .OrderBy(p => p.Estado == EstadoProducto.Agotado).ThenByDescending(p => p.FechaCreacion)
            .ToListAsync();

        return new CatalogoViewModel
        {
            Productos = productos.Select(p => Tarjeta(p, p.Esal!)).ToList(),
            Categorias = categorias,
            Categoria = categoria,
            Busqueda = q
        };
    }

    private ProductoTarjeta Tarjeta(Producto p, Esal esal) => new()
    {
        Id = p.Id,
        Nombre = p.Nombre,
        Precio = p.Precio,
        Categoria = p.Categoria,
        Agotado = p.Estado == EstadoProducto.Agotado,
        FotoUrl = p.Fotos.OrderBy(f => f.Orden).ThenBy(f => f.Id).Select(f => _archivos.UrlPublica(f.Ruta)).FirstOrDefault(),
        SlugEsal = esal.Slug ?? string.Empty,
        NombreEsal = esal.Nombre
    };

    /// <summary>Fundación activa, con página pública y con el módulo Tienda activo.</summary>
    private async Task<Esal?> BuscarEsalAsync(string slug)
    {
        var esal = await _db.Esales.AsNoTracking().FirstOrDefaultAsync(e => e.Slug == slug && e.Activa);
        return esal is not null && await _modulos.EstaActivoAsync(esal.Id, CodigosModulo.Tienda) ? esal : null;
    }

    /// <summary>Fundaciones activas con el módulo Tienda (configurable) activo, en una sola consulta.</summary>
    private Task<List<int>> EsalesConTiendaAsync()
        => _db.EsalModulos.IgnoreQueryFilters().AsNoTracking()
            .Where(em => em.Activo && em.Modulo!.Codigo == CodigosModulo.Tienda && em.Esal!.Activa && em.Esal.Slug != null)
            .Select(em => em.EsalId).Distinct().ToListAsync();

    private IActionResult NoDisponible()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("NoDisponible");
    }
}
