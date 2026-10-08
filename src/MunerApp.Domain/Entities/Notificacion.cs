namespace MunerApp.Domain.Entities;

/// <summary>Notificación interna para un usuario (sistema base del Sprint 2, HU-015).</summary>
public class Notificacion
{
    public int Id { get; set; }
    public string UsuarioId { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public string? Url { get; set; }

    /// <summary>Ícono de Bootstrap Icons, por ejemplo "bi-check-circle".</summary>
    public string Icono { get; set; } = "bi-bell";
    public bool Leida { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}
