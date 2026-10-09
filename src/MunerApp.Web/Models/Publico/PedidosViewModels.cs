using MunerApp.Domain.Enums;

namespace MunerApp.Web.Models.Publico;

/// <summary>Un pedido de la tienda, como lo ven el donante (en su chat) y la fundación (HU-026).</summary>
public class PedidoItem
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public int ConversacionId { get; set; }
    public string NombreProducto { get; set; } = string.Empty;
    public string? Donante { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Total { get; set; }
    public string? Notas { get; set; }
    public EstadoPedido Estado { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }
}
