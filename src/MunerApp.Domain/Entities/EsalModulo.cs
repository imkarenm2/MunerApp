using MunerApp.Domain.Common;

namespace MunerApp.Domain.Entities;

public class EsalModulo : IPerteneceAEsal
{
    public int EsalId { get; set; }
    public int ModuloId { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaCambio { get; set; } = DateTime.UtcNow;

    public Esal? Esal { get; set; }
    public Modulo? Modulo { get; set; }
}
