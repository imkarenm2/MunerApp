namespace MunerApp.Domain.Enums;

/// <summary>
/// Se guarda como texto. Las causas que publica una fundación empiezan en <see cref="PorAprobar"/>
/// y el superadministrador las aprueba (pasan a <see cref="Activa"/>) o las rechaza.
/// </summary>
public enum EstadoCausa
{
    Activa,
    Pausada,
    Cerrada,
    PorAprobar,
    Rechazada
}
