using MunerApp.Domain.Enums;

namespace MunerApp.Domain.Entities;

/// <summary>
/// Registro de cada aviso de Wompi que llega a la plataforma (HU-044), válido o no,
/// para auditar los pagos en línea y los intentos con firma inválida.
/// </summary>
public class EventoPasarela
{
    public int Id { get; set; }

    /// <summary>Fundación de la URL a la que llegó el aviso.</summary>
    public int EsalId { get; set; }

    /// <summary>Tipo de evento, por ejemplo "transaction.updated".</summary>
    public string? Evento { get; set; }
    public string? TransaccionId { get; set; }
    public string? Referencia { get; set; }

    /// <summary>Estado de la transacción en Wompi: APPROVED, DECLINED, VOIDED, ERROR o PENDING.</summary>
    public string? EstadoTransaccion { get; set; }

    public ResultadoEventoPasarela Resultado { get; set; }
    public string? Detalle { get; set; }

    /// <summary>Donación afectada, si se procesó.</summary>
    public int? DonacionId { get; set; }

    /// <summary>Cuerpo recibido tal cual (no contiene secretos: Wompi solo envía la firma calculada).</summary>
    public string Cuerpo { get; set; } = string.Empty;

    public DateTime FechaRecepcion { get; set; } = DateTime.UtcNow;
}
