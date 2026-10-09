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
using MunerApp.Web.Models.Publico;

namespace MunerApp.Web.Controllers;

/// <summary>
/// HU-025: el donante escribe a la fundación sobre un producto de su tienda para acordar la compra.
/// Escenario 1: desde el producto inicia el chat con un primer mensaje; la fundación recibe una notificación.
/// Escenario 2: en "Mis chats" ve sus conversaciones con los mensajes sin leer y responde.
/// Escenario 3: si ya había escrito sobre ese producto, continúa la misma conversación.
/// Las conversaciones de un donante pueden ser de varias fundaciones: se consultan con IgnoreQueryFilters
/// y siempre filtrando por el usuario autenticado.
/// </summary>
[Authorize]
public class ChatsTiendaController : Controller
{
    private readonly MunerAppDbContext _db;
    private readonly IAlmacenamientoArchivos _archivos;
    private readonly IModuloService _modulos;
    private readonly INotificacionService _notificaciones;

    public ChatsTiendaController(MunerAppDbContext db, IAlmacenamientoArchivos archivos, IModuloService modulos,
        INotificacionService notificaciones)
    {
        _db = db;
        _archivos = archivos;
        _modulos = modulos;
        _notificaciones = notificaciones;
    }

    private string UsuarioId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private string NombreUsuario => User.FindFirstValue(MunerAppClaims.NombreCompleto) ?? "Un donante";

    /// <summary>Las cuentas de una fundación (administradores y voluntarios) no compran como donantes.</summary>
    private bool EsCuentaDeFundacion => User.IsInRole(Roles.AdministradorESAL) || User.IsInRole(Roles.Voluntario) || User.IsInRole(Roles.SuperAdministrador);

    // ---------- Escenario 1: iniciar el chat desde el producto ----------

    [HttpGet("fundaciones/{slug}/tienda/{id:int}/chat")]
    public async Task<IActionResult> Iniciar(string slug, int id)
    {
        var (producto, esal) = await BuscarProductoAsync(slug, id);
        if (producto is null || esal is null) return NotFound();

        if (EsCuentaDeFundacion)
        {
            TempData["Error"] = "Las cuentas de una fundación no pueden comprar en la tienda. Usa una cuenta de donante.";
            return Redirect($"/fundaciones/{slug}/tienda/{id}");
        }

        // Escenario 3: ya existe → continúa la misma conversación
        var existente = await _db.ConversacionesTienda.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.DonanteId == UsuarioId && c.ProductoId == id).Select(c => (int?)c.Id).FirstOrDefaultAsync();
        if (existente is int cid) return RedirectToAction(nameof(Chat), new { id = cid });

        if (producto.Estado != EstadoProducto.Disponible)
        {
            TempData["Error"] = "Este producto está agotado por ahora.";
            return Redirect($"/fundaciones/{slug}/tienda/{id}");
        }

        return View(Preparar(new IniciarChatViewModel
        {
            Texto = $"Hola, me interesa «{producto.Nombre}». ¿Cómo puedo comprarlo?"
        }, producto, esal));
    }

    [HttpPost("fundaciones/{slug}/tienda/{id:int}/chat")]
    public async Task<IActionResult> Iniciar(string slug, int id, IniciarChatViewModel model)
    {
        var (producto, esal) = await BuscarProductoAsync(slug, id);
        if (producto is null || esal is null) return NotFound();
        if (EsCuentaDeFundacion) return Redirect($"/fundaciones/{slug}/tienda/{id}");

        var existente = await _db.ConversacionesTienda.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.DonanteId == UsuarioId && c.ProductoId == id).Select(c => (int?)c.Id).FirstOrDefaultAsync();
        if (existente is int cid) return RedirectToAction(nameof(Chat), new { id = cid });

        if (producto.Estado != EstadoProducto.Disponible)
        {
            TempData["Error"] = "Este producto está agotado por ahora.";
            return Redirect($"/fundaciones/{slug}/tienda/{id}");
        }
        if (!ModelState.IsValid) return View(Preparar(model, producto, esal));

        var ahora = DateTime.UtcNow;
        var conversacion = new ConversacionTienda
        {
            EsalId = esal.Id,
            ProductoId = producto.Id,
            DonanteId = UsuarioId,
            FechaCreacion = ahora,
            FechaUltimoMensaje = ahora,
            UltimaLecturaDonante = ahora
        };
        conversacion.Mensajes.Add(new MensajeTienda { EsalId = esal.Id, AutorId = UsuarioId, Texto = model.Texto.Trim(), Fecha = ahora });
        _db.ConversacionesTienda.Add(conversacion);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Doble clic: la otra petición ya creó la conversación (índice único donante + producto)
            var ya = await _db.ConversacionesTienda.IgnoreQueryFilters().AsNoTracking()
                .Where(c => c.DonanteId == UsuarioId && c.ProductoId == id).Select(c => c.Id).FirstAsync();
            return RedirectToAction(nameof(Chat), new { id = ya });
        }

        await _notificaciones.AgregarAAdministradoresAsync(esal.Id, "Nuevo mensaje en la tienda",
            $"{NombreUsuario} pregunta por «{producto.Nombre}».",
            $"/Fundacion/ChatsTienda/Detalle/{conversacion.Id}", "bi-chat-heart");
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"Enviaste tu mensaje a {esal.Nombre}. Te avisaremos cuando te respondan.";
        return RedirectToAction(nameof(Chat), new { id = conversacion.Id });
    }

    // ---------- Escenario 2: mis chats ----------

    [HttpGet("mis-chats")]
    public async Task<IActionResult> Index()
    {
        var chats = await _db.ConversacionesTienda.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.DonanteId == UsuarioId)
            .OrderByDescending(c => c.FechaUltimoMensaje)
            .Select(c => new
            {
                c.Id, c.Estado, c.FechaUltimoMensaje,
                Producto = c.Producto!.Nombre,
                Foto = c.Producto.Fotos.OrderBy(f => f.Orden).ThenBy(f => f.Id).Select(f => f.Ruta).FirstOrDefault(),
                Esal = c.Esal!.Nombre,
                Ultimo = c.Mensajes.OrderByDescending(m => m.Id).Select(m => m.Texto).FirstOrDefault(),
                NoLeidos = c.Mensajes.Count(m => m.DeLaFundacion && (c.UltimaLecturaDonante == null || m.Fecha > c.UltimaLecturaDonante))
            })
            .ToListAsync();

        return View(chats.Select(c => new ChatItem
        {
            Id = c.Id,
            Estado = c.Estado,
            FechaUltimoMensaje = c.FechaUltimoMensaje,
            NombreProducto = c.Producto,
            FotoUrl = c.Foto is null ? null : _archivos.UrlPublica(c.Foto),
            NombreEsal = c.Esal,
            UltimoMensaje = c.Ultimo,
            NoLeidos = c.NoLeidos
        }).ToList());
    }

    [HttpGet("mis-chats/{id:int}")]
    public async Task<IActionResult> Chat(int id)
    {
        var c = await BuscarPropiaAsync(id);
        if (c is null) return NotFound();

        var modelo = await ModeloAsync(c);
        await MarcarLeidoAsync(id);
        return View(modelo);
    }

    [HttpPost("mis-chats/{id:int}")]
    public async Task<IActionResult> Chat(int id, [Bind(Prefix = "Nuevo")] MensajeChatForm nuevo)
    {
        var c = await BuscarPropiaAsync(id);
        if (c is null) return NotFound();

        if (!ModelState.IsValid)
        {
            var modelo = await ModeloAsync(c);
            modelo.Nuevo = nuevo;
            return View(modelo);
        }

        // Solo se avisa si la fundación ya había leído todo: si tiene mensajes sin leer, ya recibió el aviso
        var avisar = c.UltimaLecturaFundacion >= c.FechaUltimoMensaje;
        var ahora = DateTime.UtcNow;
        _db.MensajesTienda.Add(new MensajeTienda { EsalId = c.EsalId, ConversacionId = c.Id, AutorId = UsuarioId, Texto = nuevo.Texto.Trim(), Fecha = ahora });
        await _db.ConversacionesTienda.IgnoreQueryFilters().Where(x => x.Id == c.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.FechaUltimoMensaje, ahora)
                .SetProperty(x => x.UltimaLecturaDonante, ahora)
                // Si la fundación la había cerrado, un mensaje nuevo la vuelve a abrir
                .SetProperty(x => x.Estado, EstadoConversacion.Abierta));
        if (avisar)
            await _notificaciones.AgregarAAdministradoresAsync(c.EsalId, "Nuevo mensaje en la tienda",
                $"{NombreUsuario} escribió sobre «{c.Producto!.Nombre}».",
                $"/Fundacion/ChatsTienda/Detalle/{c.Id}", "bi-chat-heart");
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Chat), new { id });
    }

    /// <summary>Mensajes nuevos (para actualizar el chat sin recargar la página).</summary>
    [HttpGet("mis-chats/{id:int}/mensajes")]
    public async Task<IActionResult> Mensajes(int id, int despues)
    {
        var c = await BuscarPropiaAsync(id);
        if (c is null) return NotFound();

        var mensajes = await MensajesAsync(c.Id, despues);
        if (mensajes.Count > 0) await MarcarLeidoAsync(id);
        return PartialView("_MensajesChat", mensajes);
    }

    // ---------- Apoyo ----------

    private async Task<(Producto? Producto, Esal? Esal)> BuscarProductoAsync(string slug, int id)
    {
        var esal = await _db.Esales.AsNoTracking().FirstOrDefaultAsync(e => e.Slug == slug && e.Activa);
        if (esal is null || !await _modulos.EstaActivoAsync(esal.Id, CodigosModulo.Tienda)) return (null, null);
        var producto = await _db.Productos.IgnoreQueryFilters().AsNoTracking().Include(p => p.Fotos)
            .FirstOrDefaultAsync(p => p.Id == id && p.EsalId == esal.Id && p.Estado != EstadoProducto.Oculto);
        return (producto, esal);
    }

    private Task<ConversacionTienda?> BuscarPropiaAsync(int id)
        => _db.ConversacionesTienda.IgnoreQueryFilters().AsNoTracking()
            .Include(c => c.Esal).Include(c => c.Producto!).ThenInclude(p => p.Fotos)
            .FirstOrDefaultAsync(c => c.Id == id && c.DonanteId == UsuarioId);

    private Task MarcarLeidoAsync(int id)
        => _db.ConversacionesTienda.IgnoreQueryFilters().Where(c => c.Id == id && c.DonanteId == UsuarioId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.UltimaLecturaDonante, DateTime.UtcNow));

    private async Task<List<MensajeChatItem>> MensajesAsync(int conversacionId, int despues)
    {
        var esal = await _db.ConversacionesTienda.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.Id == conversacionId).Select(c => c.Esal!.Nombre).FirstAsync();
        return await _db.MensajesTienda.IgnoreQueryFilters().AsNoTracking()
            .Where(m => m.ConversacionId == conversacionId && m.Id > despues)
            .OrderBy(m => m.Id)
            .Select(m => new MensajeChatItem(m.Id, m.Texto, m.Fecha, !m.DeLaFundacion, m.DeLaFundacion ? esal : "Tú"))
            .ToListAsync();
    }

    private async Task<ChatDonanteViewModel> ModeloAsync(ConversacionTienda c)
    {
        var p = c.Producto!;
        return new ChatDonanteViewModel
        {
            Id = c.Id,
            Estado = c.Estado,
            ProductoId = p.Id,
            NombreProducto = p.Nombre,
            Precio = p.Precio,
            FotoUrl = p.Fotos.OrderBy(f => f.Orden).ThenBy(f => f.Id).Select(f => _archivos.UrlPublica(f.Ruta)).FirstOrDefault(),
            ProductoVisible = p.Estado != EstadoProducto.Oculto,
            SlugEsal = c.Esal!.Slug ?? string.Empty,
            NombreEsal = c.Esal.Nombre,
            LogoUrl = c.Esal.LogoRuta is null ? null : _archivos.UrlPublica(c.Esal.LogoRuta),
            Mensajes = await MensajesAsync(c.Id, 0)
        };
    }

    private IniciarChatViewModel Preparar(IniciarChatViewModel model, Producto p, Esal esal)
    {
        model.ProductoId = p.Id;
        model.NombreProducto = p.Nombre;
        model.Precio = p.Precio;
        model.FotoUrl = p.Fotos.OrderBy(f => f.Orden).ThenBy(f => f.Id).Select(f => _archivos.UrlPublica(f.Ruta)).FirstOrDefault();
        model.SlugEsal = esal.Slug!;
        model.NombreEsal = esal.Nombre;
        model.LogoUrl = esal.LogoRuta is null ? null : _archivos.UrlPublica(esal.LogoRuta);
        return model;
    }
}
