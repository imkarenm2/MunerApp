namespace MunerApp.Application.Interfaces;

/// <summary>Datos de la fundación del usuario autenticado (se leen de sus claims).</summary>
public interface IEsalActual
{
    bool EstaAutenticado { get; }
    bool EsSuperAdmin { get; }

    /// <summary>Nulo para visitantes, donantes y superadministrador.</summary>
    int? EsalId { get; }
}
