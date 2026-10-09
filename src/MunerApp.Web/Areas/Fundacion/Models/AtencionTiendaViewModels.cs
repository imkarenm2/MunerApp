using System.ComponentModel.DataAnnotations;
using MunerApp.Domain.Enums;
using MunerApp.Web.Models.Publico;

namespace MunerApp.Web.Areas.Fundacion.Models;

/// <summary>HU-026: una conversación en la bandeja de la fundación.</summary>
public class ChatEsalItem
{
    public int Id { get; set; }
    public string Donante { get; set; } = string.Empty;
    public string NombreProducto { get; set; } = string.Empty;
    public string? FotoUrl { get; set; }
    public string? UltimoMensaje { get; set; }
    public bool UltimoEsDeLaFundacion { get; set; }
    public DateTime FechaUltimoMensaje { get; set; }
    public int NoLeidos { get; set; }
    public EstadoConversacion Estado { get; set; }
}

public class ChatsEsalIndexViewModel
{
    public EstadoConversacion Estado { get; set; }
    public int Abiertas { get; set; }
    public int Cerradas { get; set; }
    public int SinLeer { get; set; }
    public IReadOnlyList<ChatEsalItem> Chats { get; set; } = Array.Empty<ChatEsalItem>();
}

/// <summary>Formulario para registrar un pedido acordado en el chat.</summary>
public class PedidoFormViewModel
{
    [Required(ErrorMessage = "Ingresa la cantidad.")]
    [Range(1, 1000, ErrorMessage = "La cantidad debe estar entre {1} y {2}.")]
    [Display(Name = "Cantidad")]
    public int? Cantidad { get; set; } = 1;

    /// <summary>Se recibe como texto para aceptar "35.000" (pesos sin decimales).</summary>
    [Required(ErrorMessage = "Ingresa el precio por unidad.")]
    [Display(Name = "Precio por unidad")]
    public string PrecioUnitario { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Las notas pueden tener máximo {1} caracteres.")]
    [Display(Name = "Notas (opcional)")]
    public string? Notas { get; set; }
}

public class ChatEsalDetalleViewModel
{
    public int Id { get; set; }
    public EstadoConversacion Estado { get; set; }
    public string Donante { get; set; } = string.Empty;
    public string? CorreoDonante { get; set; }
    public int ProductoId { get; set; }
    public string NombreProducto { get; set; } = string.Empty;
    public decimal PrecioProducto { get; set; }
    public EstadoProducto EstadoProducto { get; set; }
    public string? FotoUrl { get; set; }
    public string? Slug { get; set; }
    public IReadOnlyList<MensajeChatItem> Mensajes { get; set; } = Array.Empty<MensajeChatItem>();
    public IReadOnlyList<PedidoItem> Pedidos { get; set; } = Array.Empty<PedidoItem>();
    public MensajeChatForm Nuevo { get; set; } = new();
    public PedidoFormViewModel Pedido { get; set; } = new();

    /// <summary>Abre el formulario de pedido al volver con errores.</summary>
    public bool MostrarFormularioPedido { get; set; }
}

public class PedidosIndexViewModel
{
    public EstadoPedido? Estado { get; set; }
    public IReadOnlyDictionary<EstadoPedido, int> Conteos { get; set; } = new Dictionary<EstadoPedido, int>();
    public decimal TotalEntregado { get; set; }
    public IReadOnlyList<PedidoItem> Pedidos { get; set; } = Array.Empty<PedidoItem>();
}
