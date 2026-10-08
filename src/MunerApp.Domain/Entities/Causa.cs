using MunerApp.Domain.Common;
using MunerApp.Domain.Enums;

namespace MunerApp.Domain.Entities;

/// <summary>
/// Causa de recaudación (vaki): una campaña con meta, fecha límite y barra de progreso (HU-041).
/// El monto recaudado no se guarda: es la suma de las donaciones confirmadas de la causa
/// (<see cref="Donacion.CausaId"/>), así el progreso siempre es consistente con las donaciones.
/// </summary>
public class Causa : IPerteneceAEsal
{
    public int Id { get; set; }
    public int EsalId { get; set; }

    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>Meta de recaudo en pesos.</summary>
    public decimal Meta { get; set; }

    /// <summary>Último día en que la causa recibe donaciones.</summary>
    public DateTime FechaLimite { get; set; }

    /// <summary>Activa o Pausada. "Cerrada" se calcula con <see cref="EstaCerrada"/> y la guardará el cierre automático (HU-046).</summary>
    public EstadoCausa Estado { get; set; } = EstadoCausa.Activa;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public string? CreadaPorId { get; set; }

    /// <summary>Documento de rendición de cuentas de la causa, cuando ya existe (HU-042, escenario 3).</summary>
    public int? RendicionDocumentoId { get; set; }

    public Esal? Esal { get; set; }
    public ICollection<FotoCausa> Fotos { get; set; } = new List<FotoCausa>();

    /// <summary>Cerrada: llegó a la meta o pasó su fecha límite.</summary>
    public bool EstaCerrada(decimal recaudado, DateTime hoy)
        => Estado == EstadoCausa.Cerrada || recaudado >= Meta || FechaLimite.Date < hoy.Date;

    /// <summary>Solo una causa activa (no pausada ni cerrada) recibe donaciones.</summary>
    public bool RecibeDonaciones(decimal recaudado, DateTime hoy)
        => Estado == EstadoCausa.Activa && !EstaCerrada(recaudado, hoy);

    /// <summary>Días que faltan para la fecha límite (0 el último día).</summary>
    public int DiasRestantes(DateTime hoy) => Math.Max(0, (FechaLimite.Date - hoy.Date).Days);
}
