using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Constantes;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Areas.Fundacion.Models;
using MunerApp.Web.Filtros;
using MunerApp.Web.Models.Publico;
using MunerApp.Web.Servicios;

namespace MunerApp.Web.Areas.Fundacion.Controllers;

/// <summary>
/// HU-026: los administradores de la ESAL atienden los chats de la tienda.
/// Escenario 1: bandeja con las conversaciones abiertas, las que tienen mensajes sin leer primero.
/// Escenario 2: responden al donante, que recibe una notificación.
/// Escenario 3: registran el pedido acordado (cantidad y precio) y lo siguen en <see cref="PedidosController"/>.
/// Escenario 4: cierran la conversación cuando terminan; si el donante vuelve a escribir, se reabre.
/// El filtro global por ESAL impide ver chats de otra fundación.
/// </summary>
[Area("Fundacion")]
[Authorize(Roles = Roles.AdministradorESAL)]
[RequiereModulo(CodigosModulo.Tienda)]
public class ChatsTiendaController : Controller
{
    private readonly MunerAppDbContext _db;
    private readonly IEsalActual _esalActual;
    private readonly IAlmacenamientoArchivos _archivos;
    private readonly INotificacionService _notificaciones;

    public ChatsTiendaController(MunerAppDbContext db, IEsalActual esalActual, IAlmacenamientoArchivos archivos,
        INotificacionService notificaciones)
    {
        _db = db;
        _esalActual = esalActual;
        _archivos = archivos;
        _notificaciones = notificaciones;
    }

    private string UsuarioId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private int EsalId => _esalActual.EsalId ?? throw new InvalidOperationException("El usuario no pertenece a una ESAL.");

    // ---------- Escenario 1: bandeja ----------

    [HttpGet]
    public async Task<IActionResult> Index(EstadoConversacion estado = EstadoConversacion.Abierta)
    {
        var chats = await _db.ConversacionesTienda.AsNoTracking()
            .Where(c => c.Estado == estado)
            .Select(c => new
            {
                c.Id, c.Estado, c.FechaUltimoMensaje,
                Donante = _db.Users.Where(u => u.Id == c.DonanteId).Select(u => u.NombreCompleto).FirstOrDefault(),
                Producto = c.Producto!.Nombre,
                Foto = c.Producto.Fotos.OrderBy(f => f.Orden).ThenBy(f => f.Id).Select(f => f.Ruta).FirstOrDefault(),
                Ultimo = c.Mensajes.OrderByDescending(m => m.Id).Select(m => new { m.Texto, m.DeLaFundacion }).FirstOrDefault(),
                NoLeidos = c.Mensajes.Count(m => !m.DeLaFundacion && (c.UltimaLecturaFundacion == null || m.Fecha > c.UltimaLecturaFundacion))
            })
            .ToListAsync();

        var conteos = await _db.ConversacionesTienda.GroupBy(c => c.Estado).Select(g => new { g.Key, Total = g.Count() }).ToListAsync();

        return View(new ChatsEsalIndexViewModel
        {
            Estado = estado,
            Abiertas = conteos.FirstOrDefault(c => c.Key == EstadoConversacion.Abierta)?.Total ?? 0,
            Cerradas = conteos.FirstOrDefault(c => c.Key == EstadoConversacion.Cerrada)?.Total ?? 0,
            SinLeer = chats.Count(c => c.NoLeidos > 0),
            Chats = chats
                .OrderByDescending(c => c.NoLeidos > 0).ThenByDescending(c => c.FechaUltimoMensaje)
                .Select(c => new ChatEsalItem
                {
                    Id = c.Id,
                    Estado = c.Estado,
                    FechaUltimoMensaje = c.FechaUltimoMensaje,
                    Donante = c.Donante ?? "Donante",
                    NombreProducto = c.Producto,
                    FotoUrl = c.Foto is null ? null : _archivos.UrlPublica(c.Foto),
                    UltimoMensaje = c.Ultimo?.Texto,
                    UltimoEsDeLaFundacion = c.Ultimo?.DeLaFundacion ?? false,
                    NoLeidos = c.NoLeidos
                }).ToList()
        });
    }

    // ---------- Escenario 2: ver y responder ----------

    [HttpGet]
    public async Task<IActionResult> Detalle(int id)
    {
        var c = await BuscarAsync(id);
        if (c is null) return NotFound();

        var modelo = await ModeloAsync(c);
        modelo.Pedido.PrecioUnitario = ((long)c.Producto!.Precio).ToString("N0", new System.Globalization.CultureInfo("es-CO"));
        await MarcarLeidoAsync(id);
        return View(modelo);
    }

    [HttpPost]
    public async Task<IActionResult> Responder(int id, [Bind(Prefix = "Nuevo")] MensajeChatForm nuevo)
    {
        var c = await BuscarAsync(id);
        if (c is null) return NotFound();

        if (!ModelState.IsValid)
        {
            var modelo = await ModeloAsync(c);
            modelo.Nuevo = nuevo;
            return View(nameof(Detalle), modelo);
        }

        // Solo se avisa si el donante ya había leído todo: si tiene mensajes sin leer, ya recibió el aviso
        var avisar = c.UltimaLecturaDonante >= c.FechaUltimoMensaje;
        await AgregarMensajeAsync(c, nuevo.Texto.Trim());
        if (avisar)
            _notificaciones.Agregar(c.DonanteId, $"{c.Esal!.Nombre} te respondió",
                $"Sobre «{c.Producto!.Nombre}»: {Recortar(nuevo.Texto.Trim(), 120)}",
                $"/mis-chats/{c.Id}", "bi-chat-heart");
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Detalle), new { id });
    }

    /// <summary>Mensajes nuevos (para actualizar el chat sin recargar la página).</summary>
    [HttpGet]
    public async Task<IActionResult> Mensajes(int id, int despues)
    {
        var c = await BuscarAsync(id);
        if (c is null) return NotFound();

        var mensajes = await MensajesAsync(c.Id, despues);
        if (mensajes.Count > 0) await MarcarLeidoAsync(id);
        return PartialView("_MensajesChat", mensajes);
    }

    // ---------- Escenario 3: registrar el pedido acordado ----------

    [HttpPost]
    public async Task<IActionResult> RegistrarPedido(int id, [Bind(Prefix = "Pedido")] PedidoFormViewModel pedido)
    {
        var c = await BuscarAsync(id);
        if (c is null) return NotFound();

        var precio = Formatos.LeerPesos(pedido.PrecioUnitario);
        if (!string.IsNullOrWhiteSpace(pedido.PrecioUnitario) && (precio is null || precio <= 0))
            ModelState.AddModelError("Pedido.PrecioUnitario", "Escribe el precio solo con números, por ejemplo 35.000.");
        else if (precio > 50_000_000)
            ModelState.AddModelError("Pedido.PrecioUnitario", "El precio por unidad no puede superar $ 50.000.000.");

        if (!ModelState.IsValid)
        {
            var modelo = await ModeloAsync(c);
            modelo.Pedido = pedido;
            modelo.MostrarFormularioPedido = true;
            return View(nameof(Detalle), modelo);
        }

        await using var transaccion = await _db.Database.BeginTransactionAsync();
        var nuevo = new Pedido
        {
            EsalId = c.EsalId,
            Codigo = "TMP-" + Guid.NewGuid().ToString("N")[..16],
            ConversacionId = c.Id,
            ProductoId = c.ProductoId,
            DonanteId = c.DonanteId,
            Cantidad = pedido.Cantidad!.Value,
            PrecioUnitario = precio!.Value,
            Total = precio.Value * pedido.Cantidad.Value,
            Notas = string.IsNullOrWhiteSpace(pedido.Notas) ? null : pedido.Notas.Trim(),
            RegistradoPorId = UsuarioId
        };
        _db.Pedidos.Add(nuevo);
        await _db.SaveChangesAsync();
        nuevo.Codigo = $"PED-{DateTime.UtcNow:yyyy}-{nuevo.Id:D6}";

        // Queda también en el chat, para que el donante vea lo acordado
        await AgregarMensajeAsync(c, $"Registramos tu pedido {nuevo.Codigo}: {nuevo.Cantidad} × «{c.Producto!.Nombre}» por {Formatos.Pesos(nuevo.Total)}."
            + (nuevo.Notas is null ? "" : $" {nuevo.Notas}"));
        _notificaciones.Agregar(c.DonanteId, "Tu pedido quedó registrado",
            $"{c.Esal!.Nombre} registró tu pedido {nuevo.Codigo} por {Formatos.Pesos(nuevo.Total)}.",
            $"/mis-chats/{c.Id}", "bi-bag-check");
        await _db.SaveChangesAsync();
        await transaccion.CommitAsync();

        TempData["Mensaje"] = $"Registraste el pedido {nuevo.Codigo} por {Formatos.Pesos(nuevo.Total)}. Síguelo en Pedidos.";
        return RedirectToAction(nameof(Detalle), new { id });
    }

    // ---------- Escenario 4: cerrar y reabrir ----------

    [HttpPost]
    public Task<IActionResult> Cerrar(int id) => CambiarEstadoAsync(id, EstadoConversacion.Cerrada);

    [HttpPost]
    public Task<IActionResult> Reabrir(int id) => CambiarEstadoAsync(id, EstadoConversacion.Abierta);

    private async Task<IActionResult> CambiarEstadoAsync(int id, EstadoConversacion estado)
    {
        var c = await _db.ConversacionesTienda.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();

        c.Estado = estado;
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = estado == EstadoConversacion.Cerrada
            ? "Cerraste la conversación. Si el donante vuelve a escribir, se abre de nuevo."
            : "Reabriste la conversación.";
        return estado == EstadoConversacion.Cerrada ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Detalle), new { id });
    }

    // ---------- Apoyo ----------

    private Task<ConversacionTienda?> BuscarAsync(int id)
        => _db.ConversacionesTienda.AsNoTracking()
            .Include(c => c.Esal).Include(c => c.Producto!).ThenInclude(p => p.Fotos)
            .FirstOrDefaultAsync(c => c.Id == id);

    private Task MarcarLeidoAsync(int id)
        => _db.ConversacionesTienda.Where(c => c.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.UltimaLecturaFundacion, DateTime.UtcNow));

    /// <summary>Agrega un mensaje de la fundación (se guarda con el siguiente SaveChanges) y actualiza la conversación.</summary>
    private async Task AgregarMensajeAsync(ConversacionTienda c, string texto)
    {
        var ahora = DateTime.UtcNow;
        _db.MensajesTienda.Add(new MensajeTienda { EsalId = c.EsalId, ConversacionId = c.Id, AutorId = UsuarioId, DeLaFundacion = true, Texto = texto, Fecha = ahora });
        await _db.ConversacionesTienda.Where(x => x.Id == c.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.FechaUltimoMensaje, ahora)
                .SetProperty(x => x.UltimaLecturaFundacion, ahora)
                .SetProperty(x => x.Estado, EstadoConversacion.Abierta));
    }

    private async Task<List<MensajeChatItem>> MensajesAsync(int conversacionId, int despues)
    {
        var autores = new Dictionary<string, string>();
        var mensajes = await _db.MensajesTienda.AsNoTracking()
            .Where(m => m.ConversacionId == conversacionId && m.Id > despues)
            .OrderBy(m => m.Id)
            .Select(m => new { m.Id, m.Texto, m.Fecha, m.DeLaFundacion, m.AutorId })
            .ToListAsync();

        // Nombre de quien escribió cada mensaje (el equipo de la fundación puede ser de varias personas)
        var ids = mensajes.Select(m => m.AutorId).Distinct().ToList();
        foreach (var u in await _db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).Select(u => new { u.Id, u.NombreCompleto }).ToListAsync())
            autores[u.Id] = u.NombreCompleto;

        return mensajes.Select(m => new MensajeChatItem(m.Id, m.Texto, m.Fecha, m.DeLaFundacion,
            m.AutorId == UsuarioId ? "Tú" : autores.GetValueOrDefault(m.AutorId, m.DeLaFundacion ? "Fundación" : "Donante"))).ToList();
    }

    private async Task<ChatEsalDetalleViewModel> ModeloAsync(ConversacionTienda c)
    {
        var donante = await _db.Users.AsNoTracking().Where(u => u.Id == c.DonanteId)
            .Select(u => new { u.NombreCompleto, u.Email }).FirstOrDefaultAsync();
        var p = c.Producto!;
        return new ChatEsalDetalleViewModel
        {
            Id = c.Id,
            Estado = c.Estado,
            Donante = donante?.NombreCompleto ?? "Donante",
            CorreoDonante = donante?.Email,
            ProductoId = p.Id,
            NombreProducto = p.Nombre,
            PrecioProducto = p.Precio,
            EstadoProducto = p.Estado,
            FotoUrl = p.Fotos.OrderBy(f => f.Orden).ThenBy(f => f.Id).Select(f => _archivos.UrlPublica(f.Ruta)).FirstOrDefault(),
            Slug = c.Esal!.Slug,
            Mensajes = await MensajesAsync(c.Id, 0),
            Pedidos = await ConsultasPedidos.DeConversacionAsync(_db.Pedidos.AsNoTracking(), c.Id)
        };
    }

    private static string Recortar(string texto, int maximo) => texto.Length <= maximo ? texto : texto[..maximo] + "…";
}
