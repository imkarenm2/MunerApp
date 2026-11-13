using MunerApp.Domain.Common;
using MunerApp.Domain.Enums;

namespace MunerApp.Domain.Entities;

/// <summary>Entrada, venta, devolución o ajuste del inventario de un producto de la tienda. No se edita ni se borra.</summary>
public class MovimientoProducto : IPerteneceAEsal
{
    public int Id { get; set; }
    public int EsalId { get; set; }
    public int ProductoId { get; set; }

    public TipoMovimientoProducto Tipo { get; set; }

    /// <summary>Unidades que entran (positivo) o salen (negativo).</summary>
    public int Cantidad { get; set; }

    /// <summary>Unidades disponibles después del movimiento.</summary>
    public int CantidadResultante { get; set; }

    /// <summary>Pedido que causó la venta o la devolución (opcional).</summary>
    public int? PedidoId { get; set; }
    public string? Nota { get; set; }

    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public string? RegistradoPorId { get; set; }

    public Producto? Producto { get; set; }
    public Pedido? Pedido { get; set; }
}
