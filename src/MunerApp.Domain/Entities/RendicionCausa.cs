using MunerApp.Domain.Common;

namespace MunerApp.Domain.Entities;

/// <summary>
/// Rendición de cuentas de una causa cerrada (HU-046): en qué se usó el dinero, con fotos y soportes.
/// Se publica una sola vez y queda en la causa, en la sección de transparencia (un PDF generado) y en el boletín.
/// Los archivos son públicos: su propósito es que cualquiera pueda verificar el uso del dinero.
/// </summary>
public class RendicionCausa : IPerteneceAEsal
{
    public const int MaxFotos = 5;
    public const int MaxSoportes = 5;

    public int Id { get; set; }
    public int EsalId { get; set; }
    public int CausaId { get; set; }

    public string Texto { get; set; } = string.Empty;

    /// <summary>Cuánto se usó, si la fundación lo indica (puede diferir de lo recaudado).</summary>
    public decimal? ValorEjecutado { get; set; }

    /// <summary>Lo recaudado al publicar, para que el registro no cambie después.</summary>
    public decimal Recaudado { get; set; }
    public int Donaciones { get; set; }

    public DateTime FechaPublicacion { get; set; } = DateTime.UtcNow;
    public string? PublicadaPorId { get; set; }

    /// <summary>PDF publicado en transparencia (también queda en <see cref="Causa.RendicionDocumentoId"/>).</summary>
    public int? DocumentoId { get; set; }

    /// <summary>Publicación del boletín (HU-027) con la categoría Rendición de cuentas.</summary>
    public int? PublicacionId { get; set; }

    public Causa? Causa { get; set; }
    public ICollection<ArchivoRendicion> Archivos { get; set; } = new List<ArchivoRendicion>();
}

/// <summary>Foto o soporte (factura, recibo) de una rendición de cuentas. Archivo público.</summary>
public class ArchivoRendicion : IPerteneceAEsal
{
    public int Id { get; set; }
    public int EsalId { get; set; }
    public int RendicionId { get; set; }
    public bool EsSoporte { get; set; }
    public string Ruta { get; set; } = string.Empty;
    public string NombreOriginal { get; set; } = string.Empty;
    public int Orden { get; set; }

    public RendicionCausa? Rendicion { get; set; }
}
