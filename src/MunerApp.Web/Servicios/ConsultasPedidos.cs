using Microsoft.EntityFrameworkCore;
using MunerApp.Domain.Entities;
using MunerApp.Web.Models.Publico;

namespace MunerApp.Web.Servicios;

/// <summary>Consultas de pedidos que comparten el panel de la fundación y el chat del donante (HU-026).</summary>
public static class ConsultasPedidos
{
    /// <summary>Pedidos de una conversación, del más reciente al más antiguo.</summary>
    public static Task<List<PedidoItem>> DeConversacionAsync(IQueryable<Pedido> pedidos, int conversacionId)
        => Proyectar(pedidos.Where(p => p.ConversacionId == conversacionId).OrderByDescending(p => p.Id)).ToListAsync();

    public static IQueryable<PedidoItem> Proyectar(IQueryable<Pedido> pedidos)
        => pedidos.Select(p => new PedidoItem
        {
            Id = p.Id,
            Codigo = p.Codigo,
            ConversacionId = p.ConversacionId,
            NombreProducto = p.Producto!.Nombre,
            Cantidad = p.Cantidad,
            PrecioUnitario = p.PrecioUnitario,
            Total = p.Total,
            Notas = p.Notas,
            Estado = p.Estado,
            FechaCreacion = p.FechaCreacion,
            FechaActualizacion = p.FechaActualizacion
        });
}
