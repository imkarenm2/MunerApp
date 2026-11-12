namespace MunerApp.Domain.Enums;

/// <summary>Movimiento del inventario de la tienda. Se guarda como texto.</summary>
public enum TipoMovimientoProducto
{
    /// <summary>La fundación agregó unidades o empezó a llevar el inventario.</summary>
    Entrada,

    /// <summary>Un pedido pasó a Pagado.</summary>
    Venta,

    /// <summary>Se canceló un pedido que ya estaba pagado.</summary>
    Devolucion,

    /// <summary>La fundación corrigió la cantidad (conteo, daño, pérdida).</summary>
    Ajuste
}
