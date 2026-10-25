using MunerApp.Domain.Common;
using MunerApp.Domain.Enums;

namespace MunerApp.Domain.Entities;

/// <summary>
/// Libro de fondos (ciclo de vida de la donación: recepción → asignación → movimientos → destinación → veeduría).
/// Cada donación confirmada queda asignada a un fondo sin guardar nada extra: si es de una causa, al fondo de esa
/// causa; si es de un apadrinamiento, al fondo de apadrinamientos; si no, al fondo general.
/// El saldo no se guarda: entradas (donaciones + traslados que llegan) − salidas (gastos + traslados que salen).
/// Los fondos se crean solos la primera vez que se registra un movimiento en ellos.
/// </summary>
public class Fondo : IPerteneceAEsal
{
    public int Id { get; set; }
    public int EsalId { get; set; }

    public string Nombre { get; set; } = string.Empty;
    public TipoFondo Tipo { get; set; }

    /// <summary>Solo para los fondos de tipo Causa. Una causa tiene un solo fondo.</summary>
    public int? CausaId { get; set; }

    public string? Descripcion { get; set; }
    public EstadoFondo Estado { get; set; } = EstadoFondo.Abierto;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaCierre { get; set; }
    public string? CreadoPorId { get; set; }

    public Esal? Esal { get; set; }
    public Causa? Causa { get; set; }
    public ICollection<MovimientoFondo> Movimientos { get; set; } = new List<MovimientoFondo>();

    public bool Abierto => Estado == EstadoFondo.Abierto;

    /// <summary>El general y el de apadrinamientos siempre existen y nunca se cierran.</summary>
    public bool SePuedeCerrar => Tipo is TipoFondo.Causa or TipoFondo.Proyecto;
}
