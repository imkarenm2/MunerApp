using MunerApp.Domain.Common;

namespace MunerApp.Domain.Entities;

/// <summary>Foto de la galería del perfil de la fundación (HU-009).</summary>
public class FotoEsal : IPerteneceAEsal
{
    public int Id { get; set; }
    public int EsalId { get; set; }
    public string Ruta { get; set; } = string.Empty;
    public int Orden { get; set; }
    public DateTime FechaCarga { get; set; } = DateTime.UtcNow;

    public Esal? Esal { get; set; }
}
