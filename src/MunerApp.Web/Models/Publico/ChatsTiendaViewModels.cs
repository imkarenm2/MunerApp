using System.ComponentModel.DataAnnotations;
using MunerApp.Domain.Enums;

namespace MunerApp.Web.Models.Publico;

/// <summary>Un mensaje del chat, visto desde un lado: <see cref="Propio"/> si lo escribió quien está mirando (HU-025, HU-026).</summary>
public record MensajeChatItem(int Id, string Texto, DateTime Fecha, bool Propio, string Autor);

/// <summary>Formulario para escribir un mensaje.</summary>
public class MensajeChatForm
{
    [Required(ErrorMessage = "Escribe tu mensaje.")]
    [StringLength(1000, ErrorMessage = "El mensaje puede tener máximo {1} caracteres.")]
    [Display(Name = "Mensaje")]
    public string Texto { get; set; } = string.Empty;
}

/// <summary>Primer mensaje al producto (HU-025, escenario 1).</summary>
public class IniciarChatViewModel : MensajeChatForm
{
    public int ProductoId { get; set; }
    public string NombreProducto { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public string? FotoUrl { get; set; }
    public string SlugEsal { get; set; } = string.Empty;
    public string NombreEsal { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
}

/// <summary>Una conversación en "Mis chats".</summary>
public class ChatItem
{
    public int Id { get; set; }
    public string NombreProducto { get; set; } = string.Empty;
    public string? FotoUrl { get; set; }
    public string NombreEsal { get; set; } = string.Empty;
    public string? UltimoMensaje { get; set; }
    public DateTime FechaUltimoMensaje { get; set; }
    public int NoLeidos { get; set; }
    public EstadoConversacion Estado { get; set; }
}

/// <summary>El chat abierto, del lado del donante.</summary>
public class ChatDonanteViewModel
{
    public int Id { get; set; }
    public EstadoConversacion Estado { get; set; }
    public int ProductoId { get; set; }
    public string NombreProducto { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public string? FotoUrl { get; set; }
    public bool ProductoVisible { get; set; }
    public string SlugEsal { get; set; } = string.Empty;
    public string NombreEsal { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public IReadOnlyList<MensajeChatItem> Mensajes { get; set; } = Array.Empty<MensajeChatItem>();
    public MensajeChatForm Nuevo { get; set; } = new();

    /// <summary>Pedidos que la fundación registró en este chat (HU-026).</summary>
    public IReadOnlyList<PedidoItem> Pedidos { get; set; } = Array.Empty<PedidoItem>();
}
