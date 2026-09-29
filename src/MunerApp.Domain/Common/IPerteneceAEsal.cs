namespace MunerApp.Domain.Common;

/// <summary>
/// Toda entidad que pertenece a una fundación implementa esta interfaz.
/// El DbContext le aplica automáticamente el filtro por ESAL.
/// </summary>
public interface IPerteneceAEsal
{
    int EsalId { get; set; }
}
