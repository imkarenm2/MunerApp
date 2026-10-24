namespace MunerApp.Application.Interfaces;

/// <summary>Datos de la fundación del usuario autenticado (se leen de sus claims).</summary>
public interface IEsalActual
{
    bool EstaAutenticado { get; }
    bool EsSuperAdmin { get; }

    /// <summary>Nulo para visitantes, donantes y superadministrador.</summary>
    int? EsalId { get; }

    /// <summary>
    /// Puede leer los datos de todas las fundaciones: el superadministrador, el veedor (consulta de solo lectura)
    /// y las tareas internas sin usuario (seed, migraciones, revisión diaria).
    /// </summary>
    bool VeTodasLasFundaciones { get; }
}
