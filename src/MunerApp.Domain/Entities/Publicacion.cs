using MunerApp.Domain.Common;
using MunerApp.Domain.Enums;

namespace MunerApp.Domain.Entities;

/// <summary>
/// Publicación del boletín de una fundación (HU-027): noticias, eventos, logros y rendiciones de cuentas.
/// Nace como borrador y solo se ve en las páginas públicas (HU-028) cuando se publica.
/// La rendición de cuentas de una causa cerrada (HU-046) se publica aquí con <see cref="CategoriaPublicacion.RendicionCuentas"/>
/// y la causa en <see cref="CausaId"/>.
/// </summary>
public class Publicacion : IPerteneceAEsal
{
    public int Id { get; set; }
    public int EsalId { get; set; }

    public string Titulo { get; set; } = string.Empty;

    /// <summary>Texto corto para las tarjetas del boletín.</summary>
    public string? Resumen { get; set; }

    public string Contenido { get; set; } = string.Empty;
    public CategoriaPublicacion Categoria { get; set; } = CategoriaPublicacion.Noticia;
    public EstadoPublicacion Estado { get; set; } = EstadoPublicacion.Borrador;

    /// <summary>Imagen principal (archivo público), opcional.</summary>
    public string? ImagenRuta { get; set; }

    /// <summary>Solo para eventos: cuándo (UTC) y dónde es.</summary>
    public DateTime? FechaEvento { get; set; }
    public string? LugarEvento { get; set; }

    /// <summary>Causa relacionada, por ejemplo la que rinde cuentas (HU-046). Opcional.</summary>
    public int? CausaId { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaActualizacion { get; set; }

    /// <summary>Cuándo se publicó por primera vez; ordena el boletín.</summary>
    public DateTime? FechaPublicacion { get; set; }
    public string? CreadaPorId { get; set; }

    public Esal? Esal { get; set; }
    public Causa? Causa { get; set; }
}
