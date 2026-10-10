namespace MunerApp.Application.Interfaces;

/// <summary>Notificaciones internas de la plataforma (base para los sprints siguientes).</summary>
public interface INotificacionService
{
    /// <summary>Agrega la notificación al contexto; se guarda con el siguiente SaveChanges.</summary>
    void Agregar(string usuarioId, string titulo, string mensaje, string? url = null, string icono = "bi-bell");

    /// <summary>Notifica a todos los administradores activos de una fundación.</summary>
    Task AgregarAAdministradoresAsync(int esalId, string titulo, string mensaje, string? url = null, string icono = "bi-bell");

    /// <summary>
    /// HU-039: notifica a los responsables de la salud de una fundación: administradores activos y voluntarios
    /// de salud (practicantes) activos.
    /// </summary>
    Task AgregarAResponsablesSaludAsync(int esalId, string titulo, string mensaje, string? url = null, string icono = "bi-bell");

    Task<int> ContarNoLeidasAsync(string usuarioId);
}
