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

    /// <summary>
    /// Activa, Pausada o Cerrada. Mientras no se guarde "Cerrada", <see cref="EstaCerrada"/> la calcula; la revisión
    /// diaria la guarda (HU-046) y el administrador también puede cerrarla.
    /// </summary>
    public EstadoCausa Estado { get; set; } = EstadoCausa.Activa;

    /// <summary>HU-046: cuándo y por qué se cerró.</summary>
    public DateTime? FechaCierre { get; set; }
    public MotivoCierreCausa? MotivoCierre { get; set; }

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

    /// <summary>
    /// HU-046 escenario 1: motivo por el que la revisión diaria debe cerrarla, o null si sigue abierta o ya está
    /// cerrada. La meta tiene prioridad: si llegó a la meta el último día, se cerró por meta.
    /// </summary>
    public MotivoCierreCausa? MotivoDeCierre(decimal recaudado, DateTime hoy)
    {
        if (Estado == EstadoCausa.Cerrada) return null;
        if (recaudado >= Meta) return MotivoCierreCausa.MetaAlcanzada;
        if (FechaLimite.Date < hoy.Date) return MotivoCierreCausa.FechaLimite;
        return null;
    }

    /// <summary>Días que faltan para la fecha límite (0 el último día).</summary>
    public int DiasRestantes(DateTime hoy) => Math.Max(0, (FechaLimite.Date - hoy.Date).Days);
}
