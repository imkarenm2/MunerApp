using MunerApp.Domain.Common;
using MunerApp.Domain.Enums;

namespace MunerApp.Domain.Entities;

/// <summary>
/// Gasto o traslado de un fondo. Nunca se edita ni se borra: si hubo un error se anula con un motivo y sigue
/// visible (tachado) en la veeduría. Un traslado son dos movimientos, uno en cada fondo, que se anulan juntos.
/// </summary>
public class MovimientoFondo : IPerteneceAEsal
{
    public int Id { get; set; }
    public int EsalId { get; set; }
    public int FondoId { get; set; }

    public TipoMovimientoFondo Tipo { get; set; }

    /// <summary>Siempre positivo; el tipo dice si suma o resta.</summary>
    public decimal Valor { get; set; }

    /// <summary>En qué se usó o por qué se traslada. Es público.</summary>
    public string Concepto { get; set; } = string.Empty;

    /// <summary>Día en que se hizo el gasto (puede ser anterior al registro).</summary>
    public DateTime Fecha { get; set; }

    /// <summary>A quién se le pagó (tienda, veterinaria...). Opcional. Nunca una persona natural con sus datos.</summary>
    public string? Proveedor { get; set; }

    /// <summary>Factura, recibo o foto. Archivo público: es la evidencia para la veeduría. Obligatorio en los gastos.</summary>
    public string? SoporteRuta { get; set; }

    /// <summary>En un traslado, el otro fondo.</summary>
    public int? FondoRelacionadoId { get; set; }

    /// <summary>En un traslado, el movimiento del otro fondo (para anularlos juntos).</summary>
    public int? MovimientoRelacionadoId { get; set; }

    public string? RegistradoPorId { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public bool Anulado { get; set; }
    public string? MotivoAnulacion { get; set; }
    public string? AnuladoPorId { get; set; }
    public DateTime? FechaAnulacion { get; set; }

    public Fondo? Fondo { get; set; }
    public Fondo? FondoRelacionado { get; set; }

    /// <summary>Lo que resta al saldo del fondo.</summary>
    public bool EsSalida => Tipo is TipoMovimientoFondo.Gasto or TipoMovimientoFondo.TrasladoSalida;
}
