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
using MunerApp.Web.Servicios;

namespace MunerApp.Web.Areas.Fundacion.Controllers;

/// <summary>
/// HU-026: seguimiento de los pedidos acordados por chat. Un pedido pasa de Acordado a Pagado y a Entregado;
/// antes de entregarse se puede cancelar. Cada cambio se le notifica al donante.
/// Si el producto lleva inventario, al pasar a Pagado se descuentan las unidades y al cancelar un pedido pagado vuelven.
/// </summary>
[Area("Fundacion")]
[Authorize(Roles = Roles.AdministradorESAL)]
[RequiereModulo(CodigosModulo.Tienda)]
public class PedidosController : Controller
{
    private readonly MunerAppDbContext _db;
    private readonly INotificacionService _notificaciones;
    private readonly InventarioTienda _inventario;

    public PedidosController(MunerAppDbContext db, INotificacionService notificaciones, InventarioTienda inventario)
    {
        _db = db;
        _notificaciones = notificaciones;
        _inventario = inventario;
    }

    [HttpGet]
    public async Task<IActionResult> Index(EstadoPedido? estado)
    {
        var consulta = _db.Pedidos.AsNoTracking();
        if (estado is EstadoPedido e) consulta = consulta.Where(p => p.Estado == e);

        var pedidos = await ConsultasPedidos.Proyectar(consulta.OrderByDescending(p => p.Id)).ToListAsync();

        // Nombre del donante de cada pedido
        var donantes = await consulta
            .Select(p => new { p.Id, Nombre = _db.Users.Where(u => u.Id == p.DonanteId).Select(u => u.NombreCompleto).FirstOrDefault() })
            .ToDictionaryAsync(x => x.Id, x => x.Nombre);
        foreach (var p in pedidos) p.Donante = donantes.GetValueOrDefault(p.Id);

        return View(new PedidosIndexViewModel
        {
            Estado = estado,
            Pedidos = pedidos,
            Conteos = await _db.Pedidos.GroupBy(p => p.Estado).Select(g => new { g.Key, Total = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Total),
            TotalEntregado = await _db.Pedidos.Where(p => p.Estado == EstadoPedido.Entregado).SumAsync(p => (decimal?)p.Total) ?? 0
        });
    }

    [HttpPost]
    public async Task<IActionResult> CambiarEstado(int id, EstadoPedido estado, string? volverA)
    {
        var p = await _db.Pedidos.Include(x => x.Producto).Include(x => x.Conversacion!).ThenInclude(c => c.Esal)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();

        if (!Pedido.Siguientes(p.Estado).Contains(estado))
        {
            TempData["Error"] = $"El pedido {p.Codigo} está {Textos.De(p.Estado).ToLower()} y no puede pasar a {Textos.De(estado).ToLower()}.";
        }
        else
        {
            await using var transaccion = await _db.Database.BeginTransactionAsync();

            // Inventario: se descuenta al pagar y vuelve si se cancela un pedido ya pagado
            var efecto = Pedido.EfectoEnInventario(p.Estado, estado);
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var inventario = efecto switch
            {
                < 0 => await _inventario.VenderAsync(p.Producto!, p.Cantidad, p, usuarioId),
                > 0 => await _inventario.DevolverAsync(p.Producto!, p.Cantidad, p, usuarioId),
                _ => new MovimientoInventario(ResultadoInventario.SinInventario, null)
            };
            if (inventario.Resultado is ResultadoInventario.Insuficiente or ResultadoInventario.SuperaMaximo)
            {
                TempData["Error"] = inventario.Resultado == ResultadoInventario.Insuficiente
                    ? $"No se pudo marcar como pagado el pedido {p.Codigo}: pide {p.Cantidad} y solo quedan {inventario.Cantidad} de «{p.Producto!.Nombre}». Actualiza el inventario del producto o ajusta el pedido."
                    : $"No se pudo cancelar el pedido {p.Codigo}: al devolver las unidades «{p.Producto!.Nombre}» superaría el tope de {InventarioTienda.CantidadMaxima:N0} unidades.";
                return Volver(volverA, p.ConversacionId);
            }

            p.Estado = estado;
            p.FechaActualizacion = DateTime.UtcNow;
            var esal = p.Conversacion!.Esal!.Nombre;
            var (titulo, mensaje) = estado switch
            {
                EstadoPedido.Pagado => ("Recibimos tu pago", $"{esal} confirmó el pago de tu pedido {p.Codigo}. ¡Gracias por ayudar!"),
                EstadoPedido.Entregado => ("Tu pedido fue entregado", $"{esal} marcó como entregado tu pedido {p.Codigo} ({p.Producto!.Nombre})."),
                _ => ("Tu pedido fue cancelado", $"{esal} canceló tu pedido {p.Codigo}. Si tienes dudas, escríbele por el chat.")
            };
            _notificaciones.Agregar(p.DonanteId, titulo, mensaje, $"/mis-chats/{p.ConversacionId}", estado == EstadoPedido.Cancelado ? "bi-x-circle" : "bi-bag-check");
            await _db.SaveChangesAsync();
            await transaccion.CommitAsync();
            TempData["Mensaje"] = $"El pedido {p.Codigo} quedó {Textos.De(estado).ToLower()}." + inventario switch
            {
                { Resultado: ResultadoInventario.Hecho, Cantidad: 0 } => $" «{p.Producto!.Nombre}» se quedó sin unidades y quedó agotado en la tienda.",
                { Resultado: ResultadoInventario.Hecho, Cantidad: int quedan } => $" Quedan {quedan} unidades de «{p.Producto!.Nombre}».",
                _ => ""
            };
        }

        return Volver(volverA, p.ConversacionId);
    }

    /// <summary>Se puede cambiar desde la lista de pedidos o desde el chat.</summary>
    private IActionResult Volver(string? volverA, int conversacionId) => volverA == "chat"
        ? RedirectToAction("Detalle", "ChatsTienda", new { id = conversacionId })
        : RedirectToAction(nameof(Index));
}
