namespace MunerApp.Application.Interfaces;

/// <summary>La persona que hace la petición actual (para el registro de auditoría).</summary>
public interface IUsuarioActual
{
    /// <summary>Null si no hay sesión o si el cambio lo hace el sistema (tareas en segundo plano, datos de ejemplo).</summary>
    string? Id { get; }
    string? Nombre { get; }
}
