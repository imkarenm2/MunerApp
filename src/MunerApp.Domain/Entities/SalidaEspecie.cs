using MunerApp.Domain.Common;

namespace MunerApp.Domain.Entities;

/// <summary>
/// Destinación de una donación en especie: qué se hizo con lo recibido (por ejemplo, "10 kg de concentrado para
/// el refugio de Funza"). Cada donación recibida es un lote; lo disponible es lo recibido menos sus salidas.
/// No se edita ni se borra: si hubo un error se anula con un motivo.
/// </summary>
public class SalidaEspecie : IPerteneceAEsal
{
    public int Id { get; set; }
    public int EsalId { get; set; }

    /// <summary>El lote: la donación en especie recibida.</summary>
    public int DonacionEspecieId { get; set; }

    public int Cantidad { get; set; }

    /// <summary>En qué se usó. Es público.</summary>
    public string Uso { get; set; } = string.Empty;

    public DateTime Fecha { get; set; }

    /// <summary>Foto del uso (evidencia pública). Opcional.</summary>
    public string? FotoRuta { get; set; }

    public string? RegistradoPorId { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public bool Anulado { get; set; }
    public string? MotivoAnulacion { get; set; }
    public string? AnuladoPorId { get; set; }
    public DateTime? FechaAnulacion { get; set; }

    public DonacionEspecie? DonacionEspecie { get; set; }
}
