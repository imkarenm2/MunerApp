namespace MunerApp.Domain.Enums;

public enum EstadoFondo
{
    Abierto,

    /// <summary>Ya no recibe gastos ni traslados. Solo se cierra con saldo en cero.</summary>
    Cerrado
}
