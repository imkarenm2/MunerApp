using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Application.Seguridad;
using MunerApp.Domain.Constantes;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Areas.Fundacion.Models;
using MunerApp.Web.Filtros;
using MunerApp.Web.Seguridad;
using MunerApp.Web.Servicios;
using MunerApp.Web.Validacion;

namespace MunerApp.Web.Areas.Fundacion.Controllers;

/// <summary>
/// HU-023: productos de la tienda. Los administradores de la ESAL ven el listado; crear, editar, cambiar el estado
/// y eliminar es del administrador principal, porque el catálogo es contenido público de la fundación.
/// Requiere el módulo Tienda. El filtro global por ESAL impide ver o modificar productos de otra fundación.
/// El inventario es opcional: si el producto tiene unidades, los pedidos pagados las descuentan (<see cref="InventarioTienda"/>).
/// </summary>
[Area("Fundacion")]
[Authorize(Roles = Roles.AdministradorESAL)]
[RequiereModulo(CodigosModulo.Tienda)]
public class ProductosController : Controller
{
    public const int MaxFotos = 4;
    private const long LimitePeticion = 26L * 1024 * 1024; // 4 fotos de hasta 5 MB y los demás campos
    private const decimal PrecioMinimo = 1_000;
    private const decimal PrecioMaximo = 50_000_000;

    private readonly MunerAppDbContext _db;
    private readonly IEsalActual _esalActual;
    private readonly IAlmacenamientoArchivos _archivos;
    private readonly InventarioTienda _inventario;

    public ProductosController(MunerAppDbContext db, IEsalActual esalActual, IAlmacenamientoArchivos archivos, InventarioTienda inventario)
    {
        _db = db;
        _esalActual = esalActual;
        _archivos = archivos;
        _inventario = inventario;
    }

    private string UsuarioId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private int EsalId => _esalActual.EsalId ?? throw new InvalidOperationException("El usuario no pertenece a una ESAL.");

    // ---------- Listado ----------

    [HttpGet]
    public async Task<IActionResult> Index(EstadoProducto? estado)
    {
        var productos = await _db.Productos.AsNoTracking().Include(p => p.Fotos)
            .OrderByDescending(p => p.FechaCreacion).ToListAsync();

        return View(new ProductosIndexViewModel
        {
            Slug = await _db.Esales.AsNoTracking().Where(e => e.Id == EsalId).Select(e => e.Slug).FirstOrDefaultAsync(),
            PuedeGestionar = User.HasClaim(MunerAppClaims.Perfil, Perfiles.Principal),
            Filtro = estado,
            Disponibles = productos.Count(p => p.Estado == EstadoProducto.Disponible),
            Agotados = productos.Count(p => p.Estado == EstadoProducto.Agotado),
            Ocultos = productos.Count(p => p.Estado == EstadoProducto.Oculto),
            Productos = productos
                .Where(p => estado is null || p.Estado == estado)
                .Select(p => new ProductoItem
                {
                    Id = p.Id,
                    Nombre = p.Nombre,
                    Precio = p.Precio,
                    Categoria = p.Categoria,
                    Estado = p.Estado,
                    Cantidad = p.Cantidad,
                    FechaCreacion = p.FechaCreacion,
                    FotoUrl = p.Fotos.OrderBy(f => f.Orden).ThenBy(f => f.Id).Select(f => _archivos.UrlPublica(f.Ruta)).FirstOrDefault()
                }).ToList()
        });
    }

    // ---------- Crear ----------

    [HttpGet]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> Crear() => View(await PrepararAsync(new ProductoFormViewModel(), null));

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    [RequestSizeLimit(LimitePeticion)]
    public async Task<IActionResult> Crear(ProductoFormViewModel model)
    {
        var precio = ValidarCampos(model);
        var fotos = (model.Fotos ?? new List<IFormFile>()).Where(f => f.Length > 0).ToList();
        var validadas = await ValidarFotosAsync(model, fotos, fotosExistentes: 0, exigirUna: true);
        if (!ModelState.IsValid) return View(await PrepararAsync(model, null));

        var producto = new Producto
        {
            EsalId = EsalId,
            Nombre = model.Nombre.Trim(),
            Descripcion = model.Descripcion.Trim(),
            Precio = precio!.Value,
            Categoria = LimpiarCategoria(model.Categoria),
            Estado = model.Cantidad == 0 && model.Estado == EstadoProducto.Disponible ? EstadoProducto.Agotado : model.Estado,
            Cantidad = model.Cantidad,
            CreadoPorId = UsuarioId
        };
        if (model.Cantidad is int unidades and > 0)
            producto.Movimientos.Add(new MovimientoProducto
            {
                EsalId = producto.EsalId,
                Tipo = TipoMovimientoProducto.Entrada,
                Cantidad = unidades,
                CantidadResultante = unidades,
                Nota = "Inventario inicial",
                RegistradoPorId = UsuarioId
            });
        await AgregarFotosAsync(producto, validadas, ordenInicial: 0);
        _db.Productos.Add(producto);
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = producto.Estado == EstadoProducto.Oculto
            ? $"Guardaste \"{producto.Nombre}\" como oculto. Cuando lo marques como disponible aparecerá en la tienda."
            : $"Publicaste \"{producto.Nombre}\". Ya aparece en la tienda de la fundación.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Editar ----------

    [HttpGet]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> Editar(int id)
    {
        var p = await _db.Productos.AsNoTracking().Include(x => x.Fotos).FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();

        return View(await PrepararAsync(new ProductoFormViewModel
        {
            Nombre = p.Nombre,
            Descripcion = p.Descripcion,
            Precio = ((long)p.Precio).ToString("N0", new System.Globalization.CultureInfo("es-CO")),
            Categoria = p.Categoria,
            Estado = p.Estado,
            Cantidad = p.Cantidad,
            CantidadOriginal = p.Cantidad
        }, p));
    }

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    [RequestSizeLimit(LimitePeticion)]
    public async Task<IActionResult> Editar(int id, ProductoFormViewModel model)
    {
        var p = await _db.Productos.Include(x => x.Fotos).FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();

        var precio = ValidarCampos(model);
        var fotos = (model.Fotos ?? new List<IFormFile>()).Where(f => f.Length > 0).ToList();
        var validadas = await ValidarFotosAsync(model, fotos, p.Fotos.Count, exigirUna: false);

        // Si alguien registró una venta mientras se editaba, no se pisa: se muestra la cantidad nueva
        var cambiaInventario = model.Cantidad != model.CantidadOriginal;
        if (cambiaInventario && p.Cantidad != model.CantidadOriginal)
            CantidadCambio(model, p.Cantidad);
        if (!ModelState.IsValid) return View(await PrepararAsync(model, p));

        await using var transaccion = await _db.Database.BeginTransactionAsync();
        if (cambiaInventario)
        {
            if (model.Cantidad is null)
                p.Cantidad = null; // deja de llevar inventario
            else if (p.Cantidad is null)
            {
                p.Cantidad = model.Cantidad;
                p.Movimientos.Add(new MovimientoProducto
                {
                    EsalId = p.EsalId,
                    Tipo = TipoMovimientoProducto.Entrada,
                    Cantidad = model.Cantidad.Value,
                    CantidadResultante = model.Cantidad.Value,
                    Nota = "Inicio del inventario",
                    RegistradoPorId = UsuarioId
                });
            }
            else
            {
                var ajuste = await _inventario.AjustarAsync(p, p.Cantidad.Value, model.Cantidad.Value, "Ajuste al editar el producto", UsuarioId);
                if (ajuste.Resultado == ResultadoInventario.Cambio)
                {
                    CantidadCambio(model, ajuste.Cantidad);
                    return View(await PrepararAsync(model, p));
                }
            }
        }

        p.Nombre = model.Nombre.Trim();
        p.Descripcion = model.Descripcion.Trim();
        p.Precio = precio!.Value;
        p.Categoria = LimpiarCategoria(model.Categoria);
        // Sin unidades no se puede pedir: queda agotado aunque se haya elegido Disponible
        var sinUnidades = p.Cantidad == 0 && model.Estado == EstadoProducto.Disponible;
        p.Estado = sinUnidades ? EstadoProducto.Agotado : model.Estado;
        p.FechaActualizacion = DateTime.UtcNow;
        await AgregarFotosAsync(p, validadas, ordenInicial: p.Fotos.Count == 0 ? 0 : p.Fotos.Max(f => f.Orden) + 1);
        await _db.SaveChangesAsync();
        await transaccion.CommitAsync();

        TempData["Mensaje"] = $"Guardaste los cambios de \"{p.Nombre}\"."
            + (sinUnidades ? " Como no tiene unidades, quedó agotado." : "");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> EliminarFoto(int id, int fotoId)
    {
        var p = await _db.Productos.Include(x => x.Fotos).FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();

        var foto = p.Fotos.FirstOrDefault(f => f.Id == fotoId);
        if (foto is null) return NotFound();
        if (p.Fotos.Count <= 1)
        {
            TempData["Error"] = "El producto debe tener al menos una foto. Sube otra antes de quitar esta.";
            return RedirectToAction(nameof(Editar), new { id });
        }

        _db.FotosProducto.Remove(foto);
        await _db.SaveChangesAsync();
        await _archivos.EliminarAsync(foto.Ruta);

        TempData["Mensaje"] = "Quitaste la foto.";
        return RedirectToAction(nameof(Editar), new { id });
    }

    // ---------- Cambio rápido de estado desde el listado ----------

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> CambiarEstado(int id, EstadoProducto estado)
    {
        var p = await _db.Productos.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        if (!Enum.IsDefined(estado)) return BadRequest();
        if (estado == EstadoProducto.Disponible && p.Cantidad == 0)
        {
            TempData["Error"] = $"\"{p.Nombre}\" no tiene unidades. Edita el producto y agrega unidades para que vuelva a estar disponible.";
            return RedirectToAction(nameof(Index));
        }
        if (estado == EstadoProducto.Agotado && p.Cantidad > 0)
        {
            TempData["Error"] = $"\"{p.Nombre}\" todavía tiene {p.Cantidad} unidades. Para marcarlo como agotado, edítalo y deja las unidades en 0; si solo quieres que no se vea, ocúltalo.";
            return RedirectToAction(nameof(Index));
        }

        p.Estado = estado;
        p.FechaActualizacion = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = estado switch
        {
            EstadoProducto.Disponible => $"\"{p.Nombre}\" está disponible en la tienda.",
            EstadoProducto.Agotado => $"Marcaste \"{p.Nombre}\" como agotado. Se sigue viendo, pero no se puede pedir.",
            _ => $"Ocultaste \"{p.Nombre}\". Ya no aparece en la tienda."
        };
        return RedirectToAction(nameof(Index));
    }

    // ---------- Eliminar ----------

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> Eliminar(int id)
    {
        var p = await _db.Productos.Include(x => x.Fotos).FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();

        // HU-025: los chats guardan de qué producto hablaban, así que un producto con chats solo se oculta
        if (await _db.ConversacionesTienda.AnyAsync(c => c.ProductoId == id))
        {
            TempData["Error"] = $"\"{p.Nombre}\" tiene chats con donantes, así que no se puede eliminar. Ocúltalo para que no aparezca en la tienda.";
            return RedirectToAction(nameof(Index));
        }

        var rutas = p.Fotos.Select(f => f.Ruta).ToList();
        _db.Productos.Remove(p);
        await _db.SaveChangesAsync();
        foreach (var ruta in rutas) await _archivos.EliminarAsync(ruta);

        TempData["Mensaje"] = $"Eliminaste \"{p.Nombre}\".";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Apoyo ----------

    /// <summary>Valida el precio y el estado. Devuelve el precio ya convertido a pesos.</summary>
    private decimal? ValidarCampos(ProductoFormViewModel model)
    {
        var precio = Formatos.LeerPesos(model.Precio);
        if (!string.IsNullOrWhiteSpace(model.Precio))
        {
            if (precio is null)
                ModelState.AddModelError(nameof(model.Precio), "Escribe el precio solo con números, por ejemplo 35.000.");
            else if (precio < PrecioMinimo)
                ModelState.AddModelError(nameof(model.Precio), $"El precio mínimo es {Formatos.Pesos(PrecioMinimo)}.");
            else if (precio > PrecioMaximo)
                ModelState.AddModelError(nameof(model.Precio), $"El precio no puede superar {Formatos.Pesos(PrecioMaximo)}.");
        }
        if (!Enum.IsDefined(model.Estado))
            ModelState.AddModelError(nameof(model.Estado), "Elige un estado válido.");
        // Agotado significa que no hay unidades: no se puede guardar agotado con unidades
        if (model.Estado == EstadoProducto.Agotado && model.Cantidad > 0)
            ModelState.AddModelError(nameof(model.Cantidad), "Un producto agotado no tiene unidades. Déjalo en 0 o márcalo como disponible para agregarlas.");
        return precio;
    }

    /// <summary>La cantidad cambió en la base de datos desde que se abrió el formulario.</summary>
    private void CantidadCambio(ProductoFormViewModel model, int? actual)
    {
        // Se muestra el valor actual y se toma como el nuevo punto de partida
        ModelState.Remove(nameof(model.Cantidad));
        ModelState.Remove(nameof(model.CantidadOriginal));
        ModelState.AddModelError(nameof(model.Cantidad), actual is int hay
            ? $"Mientras editabas cambió el inventario (por ejemplo, se pagó un pedido). Ahora hay {hay} unidades: revisa la cantidad y guarda de nuevo."
            : "Mientras editabas cambió el inventario. Revisa la cantidad y guarda de nuevo.");
        model.Cantidad = actual;
        model.CantidadOriginal = actual;
    }

    /// <summary>"  ropa " → "Ropa": así "ropa" y "Ropa" son la misma categoría en el filtro del catálogo.</summary>
    private static string? LimpiarCategoria(string? categoria)
    {
        if (string.IsNullOrWhiteSpace(categoria)) return null;
        var c = string.Join(' ', categoria.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return char.ToUpper(c[0]) + c[1..].ToLower();
    }

    private async Task<List<(IFormFile Archivo, ArchivoValidado Info)>> ValidarFotosAsync(
        ProductoFormViewModel model, List<IFormFile> fotos, int fotosExistentes, bool exigirUna)
    {
        var validadas = new List<(IFormFile, ArchivoValidado)>();
        if (exigirUna && fotos.Count == 0)
        {
            ModelState.AddModelError(nameof(model.Fotos), "Agrega al menos una foto del producto.");
            return validadas;
        }
        if (fotosExistentes + fotos.Count > MaxFotos)
        {
            ModelState.AddModelError(nameof(model.Fotos), $"Un producto puede tener máximo {MaxFotos} fotos" +
                (fotosExistentes > 0 ? $" (ya tiene {fotosExistentes})." : "."));
            return validadas;
        }
        foreach (var foto in fotos)
        {
            var info = await ValidadorArchivos.ValidarAsync(foto, TipoArchivo.Imagen);
            if (!info.Valido)
                ModelState.AddModelError(nameof(model.Fotos), $"\"{Path.GetFileName(foto.FileName)}\": {info.Error}");
            else
                validadas.Add((foto, info));
        }
        return validadas;
    }

    private async Task AgregarFotosAsync(Producto producto, List<(IFormFile Archivo, ArchivoValidado Info)> fotos, int ordenInicial)
    {
        var orden = ordenInicial;
        foreach (var (archivo, info) in fotos)
        {
            await using var stream = archivo.OpenReadStream();
            var clave = await _archivos.GuardarAsync(stream, $"esal/{producto.EsalId}/productos", info.Extension, publico: true);
            producto.Fotos.Add(new FotoProducto { EsalId = producto.EsalId, Ruta = clave, Orden = orden++ });
        }
    }

    private async Task<ProductoFormViewModel> PrepararAsync(ProductoFormViewModel model, Producto? p)
    {
        model.Id = p?.Id;
        model.FotosActuales = p is null
            ? Array.Empty<FotoProductoItem>()
            : p.Fotos.OrderBy(f => f.Orden).ThenBy(f => f.Id).Select(f => new FotoProductoItem(f.Id, _archivos.UrlPublica(f.Ruta))).ToList();
        model.CategoriasExistentes = await _db.Productos.AsNoTracking()
            .Where(x => x.Categoria != null).Select(x => x.Categoria!).Distinct().OrderBy(c => c).ToListAsync();
        model.Movimientos = p is null ? Array.Empty<MovimientoProductoItem>() : await (
            from m in _db.MovimientosProducto.AsNoTracking()
            where m.ProductoId == p.Id
            join u in _db.Users on m.RegistradoPorId equals u.Id into us
            from u in us.DefaultIfEmpty()
            orderby m.Fecha descending, m.Id descending
            select new MovimientoProductoItem(m.Fecha, m.Tipo, m.Cantidad, m.CantidadResultante, m.Nota, u == null ? null : u.NombreCompleto))
            .Take(15).ToListAsync();
        return model;
    }
}