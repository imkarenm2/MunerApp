using MunerApp.Domain.Common;
using MunerApp.Domain.Enums;

namespace MunerApp.Domain.Entities;

/// <summary>
/// Algo que la fundación necesita recibir en especie (concentrado, arena, medicamentos...).
/// Se publica en su perfil con una barra de avance: lo recibido es la suma de las donaciones en especie recibidas.
/// </summary>
public class NecesidadEspecie : IPerteneceAEsal
{
    public int Id { get; set; }
    public int EsalId { get; set; }

    public string Nombre { get; set; } = string.Empty;
    public CategoriaEspecie Categoria { get; set; }

    /// <summary>Unidad en la que se cuenta: "bultos de 8 kg", "unidades", "paquetes".</summary>
    public string Unidad { get; set; } = string.Empty;

    public int CantidadMeta { get; set; }
    public string? Descripcion { get; set; }
    public bool Urgente { get; set; }

    /// <summary>Si es false ya no se muestra en el perfil (la fundación la cerró).</summary>
    public bool Activa { get; set; } = true;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public string? CreadaPorId { get; set; }

    public Esal? Esal { get; set; }
}
