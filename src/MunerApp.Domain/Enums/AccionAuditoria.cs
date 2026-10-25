namespace MunerApp.Domain.Enums;

/// <summary>Qué le pasó a un dato en el registro de auditoría.</summary>
public enum AccionAuditoria
{
    Creado,
    Modificado,
    Eliminado,

    /// <summary>Un veedor consultó el registro de cambios: quien vigila también deja rastro.</summary>
    Consultado
}
