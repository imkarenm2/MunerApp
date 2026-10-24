using MunerApp.Domain.Enums;

namespace MunerApp.Domain.Entities;

/// <summary>
/// Trazabilidad (requerimiento no funcional): un cambio en los datos, con quién lo hizo y cuándo.
/// Lo escribe solo el interceptor de auditoría al guardar; nadie lo edita ni lo borra desde la aplicación.
/// No usa el filtro por fundación: lo consultan el veedor y el superadministrador.
/// </summary>
public class RegistroAuditoria
{
    public long Id { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    /// <summary>Quién hizo el cambio. Null cuando lo hizo el sistema (revisión diaria, datos de ejemplo, avisos de Wompi).</summary>
    public string? UsuarioId { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty;

    /// <summary>Fundación a la que pertenece el dato (si aplica).</summary>
    public int? EsalId { get; set; }

    /// <summary>Tipo de dato: Causa, Donacion, Beneficiario...</summary>
    public string Entidad { get; set; } = string.Empty;
    public string EntidadId { get; set; } = string.Empty;
    public AccionAuditoria Accion { get; set; }

    /// <summary>Campos en JSON. Al crear: sus valores; al modificar: {"Campo": ["antes", "después"]}; al eliminar: lo que tenía.</summary>
    public string Cambios { get; set; } = "{}";
}
