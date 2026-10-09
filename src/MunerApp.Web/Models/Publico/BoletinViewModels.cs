using MunerApp.Domain.Enums;

namespace MunerApp.Web.Models.Publico;

/// <summary>Tarjeta de una publicación del boletín (HU-028).</summary>
public class PublicacionTarjeta
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string? Resumen { get; set; }
    public CategoriaPublicacion Categoria { get; set; }
    public string? ImagenUrl { get; set; }
    public DateTime FechaPublicacion { get; set; }
    public DateTime? FechaEvento { get; set; }
    public string? LugarEvento { get; set; }

    public string SlugEsal { get; set; } = string.Empty;
    public string NombreEsal { get; set; } = string.Empty;

    /// <summary>En el boletín general (todas las fundaciones) la tarjeta muestra de qué fundación es.</summary>
    public bool MostrarFundacion { get; set; }

    public bool EventoPasado => FechaEvento is DateTime f && f < DateTime.UtcNow;
    public string Url => $"/fundaciones/{SlugEsal}/boletin/{Id}";
}

/// <summary>Boletín de una fundación (/fundaciones/{slug}/boletin) o de todas (/boletin).</summary>
public class BoletinPublicoViewModel
{
    /// <summary>Null en el boletín general.</summary>
    public string? Slug { get; set; }
    public string? NombreEsal { get; set; }
    public string? LogoUrl { get; set; }

    public CategoriaPublicacion? Categoria { get; set; }
    public IReadOnlyList<PublicacionTarjeta> ProximosEventos { get; set; } = Array.Empty<PublicacionTarjeta>();
    public IReadOnlyList<PublicacionTarjeta> Publicaciones { get; set; } = Array.Empty<PublicacionTarjeta>();
    public int Pagina { get; set; } = 1;
    public bool HayMas { get; set; }
}

public class PublicacionDetalleViewModel
{
    public PublicacionTarjeta Publicacion { get; set; } = new();
    public string Contenido { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }

    /// <summary>Causa relacionada, si sigue visible en la página de la fundación.</summary>
    public int? CausaId { get; set; }
    public string? TituloCausa { get; set; }

    public IReadOnlyList<PublicacionTarjeta> Otras { get; set; } = Array.Empty<PublicacionTarjeta>();

    /// <summary>Enlace para agregar el evento a Google Calendar.</summary>
    public string? UrlCalendario { get; set; }
}
