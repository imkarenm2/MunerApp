namespace MunerApp.Domain.Entities;

/// <summary>
/// Registro de cada tarea de la revisión diaria (HU-039, HU-046). La llave (fecha, tarea) garantiza que una
/// tarea se ejecute una sola vez por día aunque la aplicación se reinicie o corra en varias instancias.
/// </summary>
public class RevisionDiaria
{
    /// <summary>Día revisado, en hora de Colombia.</summary>
    public DateTime Fecha { get; set; }
    public string Tarea { get; set; } = string.Empty;

    public DateTime Iniciada { get; set; } = DateTime.UtcNow;
    public DateTime? Terminada { get; set; }

    /// <summary>Qué hizo, para revisar en la base de datos (por ejemplo "2 avisos de vencimiento").</summary>
    public string? Resumen { get; set; }
}
