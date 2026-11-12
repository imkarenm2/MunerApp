using MunerApp.Domain.Common;
using MunerApp.Domain.Enums;

namespace MunerApp.Domain.Entities;

/// <summary>
/// Compra acordada por chat entre la fundación y un donante (HU-026). La plataforma no cobra:
/// la fundación registra el pedido y actualiza su estado a medida que recibe el pago y lo entrega.
/// </summary>
public class Pedido : IPerteneceAEsal
{
    public int Id { get; set; }
    public int EsalId { get; set; }

    /// <summary>Código que ven el donante y la fundación, por ejemplo PED-2026-000012.</summary>
    public string Codigo { get; set; } = string.Empty;

    public int ConversacionId { get; set; }
    public int ProductoId { get; set; }
    public string DonanteId { get; set; } = string.Empty;

    public int Cantidad { get; set; }

    /// <summary>Precio acordado por unidad (puede ser distinto al del catálogo, por ejemplo con descuento).</summary>
    public decimal PrecioUnitario { get; set; }
    public decimal Total { get; set; }

    /// <summary>Acuerdos de pago o entrega que la fundación quiere dejar por escrito.</summary>
    public string? Notas { get; set; }

    public EstadoPedido Estado { get; set; } = EstadoPedido.Acordado;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaActualizacion { get; set; }
    public string? RegistradoPorId { get; set; }

    public ConversacionTienda? Conversacion { get; set; }
    public Producto? Producto { get; set; }

    /// <summary>Estados a los que puede pasar: Acordado → Pagado → Entregado; antes de entregarse se puede cancelar.</summary>
    public static IReadOnlyList<EstadoPedido> Siguientes(EstadoPedido estado) => estado switch
    {
        EstadoPedido.Acordado => new[] { EstadoPedido.Pagado, EstadoPedido.Cancelado },
        EstadoPedido.Pagado => new[] { EstadoPedido.Entregado, EstadoPedido.Cancelado },
        _ => Array.Empty<EstadoPedido>()
    };

    /// <summary>
    /// Qué le pasa al inventario del producto con un cambio de estado: -1 descuenta (el pedido se pagó),
    /// +1 devuelve (se canceló un pedido ya pagado) y 0 no lo toca. Acordar un pedido no aparta unidades.
    /// </summary>
    public static int EfectoEnInventario(EstadoPedido antes, EstadoPedido despues) => (antes, despues) switch
    {
        (EstadoPedido.Acordado, EstadoPedido.Pagado) => -1,
        (EstadoPedido.Pagado, EstadoPedido.Cancelado) => 1,
        _ => 0
    };
}
