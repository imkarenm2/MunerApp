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
/// </summary>
[Area("Fundacion")]
[Authorize(Roles = Roles.AdministradorESAL)]
[RequiereModulo(CodigosModulo.Tienda)]
public class PedidosController : Controller
{
    private readonly MunerAppDbContext _db;
    private readonly INotificacionService _notificaciones;

    public PedidosController(MunerAppDbContext db, INotificacionService notificaciones)
    {
        _db = db;
        _notificaciones = notificaciones;
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
            TempData["Mensaje"] = $"El pedido {p.Codigo} quedó {Textos.De(estado).ToLower()}.";
        }

        // Se puede cambiar desde la lista de pedidos o desde el chat
        return volverA == "chat"
            ? RedirectToAction("Detalle", "ChatsTienda", new { id = p.ConversacionId })
            : RedirectToAction(nameof(Index));
    }
}
