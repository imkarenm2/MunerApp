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

    public Esal? Esal { get; set; }
    public Apadrinamiento? Apadrinamiento { get; set; }
}
