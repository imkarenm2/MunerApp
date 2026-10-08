using MunerApp.Domain.Common;

namespace MunerApp.Domain.Entities;

/// <summary>Foto de una causa de recaudación. Archivo público (HU-041).</summary>
public class FotoCausa : IPerteneceAEsal
{
    public int Id { get; set; }
    public int EsalId { get; set; }
    public int CausaId { get; set; }
    public string Ruta { get; set; } = string.Empty;
    public int Orden { get; set; }
    public DateTime FechaCarga { get; set; } = DateTime.UtcNow;

    public Causa? Causa { get; set; }
}
