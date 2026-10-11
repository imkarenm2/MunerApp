using MunerApp.Domain.Common;

namespace MunerApp.Domain.Entities;

/// <summary>Foto adjunta a un evento clínico. Archivo privado: solo la ve el personal autorizado (HU-019).</summary>
public class FotoEventoClinico : IPerteneceAEsal
{
    public int Id { get; set; }
    public int EsalId { get; set; }
    public int EventoClinicoId { get; set; }
    public string Ruta { get; set; } = string.Empty;
    public DateTime FechaCarga { get; set; } = DateTime.UtcNow;

    public EventoClinico? Evento { get; set; }
}
