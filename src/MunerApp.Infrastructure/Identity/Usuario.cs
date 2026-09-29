using Microsoft.AspNetCore.Identity;
using MunerApp.Domain.Entities;

namespace MunerApp.Infrastructure.Identity;

/// <summary>Usuario de la plataforma (tabla AspNetUsers extendida).</summary>
public class Usuario : IdentityUser
{
    public string NombreCompleto { get; set; } = string.Empty;

    /// <summary>Fundación a la que pertenece. Nulo para donantes y superadministrador.</summary>
    public int? EsalId { get; set; }
    public Esal? Esal { get; set; }

    /// <summary>Principal, Consulta, Salud o General (ver Domain.Constantes.Perfiles).</summary>
    public string? Perfil { get; set; }

    public bool Activo { get; set; } = true;
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
}
