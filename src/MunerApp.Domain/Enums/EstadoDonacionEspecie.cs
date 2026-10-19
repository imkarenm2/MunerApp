namespace MunerApp.Domain.Enums;

/// <summary>
/// Ofrecida: el donante dijo qué va a llevar. Recibida: la fundación confirmó que llegó (con la cantidad real).
/// NoRecibida: no llegó o no se pudo recibir. Cancelada: el donante desistió antes de entregarla. Se guarda como texto.
/// </summary>
public enum EstadoDonacionEspecie
{
    Ofrecida,
    Recibida,
    NoRecibida,
    Cancelada
}
