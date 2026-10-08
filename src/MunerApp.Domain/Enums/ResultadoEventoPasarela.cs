namespace MunerApp.Domain.Enums;

/// <summary>Qué se hizo con un aviso (evento) de Wompi (HU-044).</summary>
public enum ResultadoEventoPasarela
{
    /// <summary>Se confirmó o rechazó la donación.</summary>
    Procesado,

    /// <summary>Escenario 2: la firma no coincide con el secreto de eventos de la fundación.</summary>
    FirmaInvalida,

    /// <summary>Escenario 3: la transacción ya se había procesado.</summary>
    Duplicado,

    /// <summary>No existe una donación en línea con esa referencia en la fundación.</summary>
    DonacionNoEncontrada,

    /// <summary>El valor pagado no coincide con el de la donación: no se confirma.</summary>
    MontoNoCoincide,

    /// <summary>Evento o estado que no cambia la donación (por ejemplo, PENDING), o fundación sin Wompi.</summary>
    Ignorado,

    /// <summary>El cuerpo no es un evento de Wompi válido.</summary>
    Invalido
}
