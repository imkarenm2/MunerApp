using MunerApp.Domain.Common;
using MunerApp.Domain.Enums;

namespace MunerApp.Domain.Entities;

/// <summary>
/// Donación hecha por llave o transferencia y reportada por el donante (HU-014).
/// La fundación la confirma o la rechaza (HU-015).
/// </summary>
public class Donacion : IPerteneceAEsal
{
    public int Id { get; set; }
    public int EsalId { get; set; }

    /// <summary>Código único que ve el donante, por ejemplo DON-2026-000123.</summary>
    public string Codigo { get; set; } = string.Empty;

    public string DonanteId { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public DateTime FechaTransferencia { get; set; }

    /// <summary>Copia de la entidad y el número a los que se transfirió, por si la fundación los cambia después.</summary>
    public string MedioPago { get; set; } = string.Empty;
    public string? ReferenciaPago { get; set; }
    public string? Mensaje { get; set; }

    /// <summary>Archivo privado: solo lo ven el donante y la fundación.</summary>
    public string SoporteRuta { get; set; } = string.Empty;

    public EstadoDonacion Estado { get; set; } = EstadoDonacion.Pendiente;
    public string? MotivoRechazo { get; set; }
    public DateTime FechaReporte { get; set; } = DateTime.UtcNow;
    public DateTime? FechaRevision { get; set; }
    public string? RevisadoPorId { get; set; }

    /// <summary>Si es un aporte de apadrinamiento (HU-021), el apadrinamiento al que pertenece.</summary>
    public int? ApadrinamientoId { get; set; }

    /// <summary>Si la donación es para una causa de recaudación (HU-041), la causa. Al confirmarse suma a su recaudado.</summary>
    public int? CausaId { get; set; }

    // ---- Donación en línea con Wompi (HU-043, HU-044) ----

    /// <summary>Manual (con soporte) o Wompi (pagada en línea; no tiene soporte).</summary>
    public OrigenDonacion Origen { get; set; } = OrigenDonacion.Manual;

    /// <summary>Referencia única que la plataforma genera y firma para el checkout de Wompi (HU-043).</summary>
    public string? ReferenciaPasarela { get; set; }

    /// <summary>Id de la transacción en Wompi, que llega en su aviso (HU-044). Evita procesarla dos veces.</summary>
    public string? TransaccionPasarelaId { get; set; }

    public Esal? Esal { get; set; }
    public Apadrinamiento? Apadrinamiento { get; set; }
    public Causa? Causa { get; set; }
}
