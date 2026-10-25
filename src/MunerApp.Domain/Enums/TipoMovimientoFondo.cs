namespace MunerApp.Domain.Enums;

/// <summary>Salidas y traslados del libro de fondos. Las entradas son las donaciones confirmadas (no se copian).</summary>
public enum TipoMovimientoFondo
{
    /// <summary>Dinero que se usó: compra, pago de un servicio... Lleva soporte público.</summary>
    Gasto,

    /// <summary>Dinero que sale de este fondo hacia otro.</summary>
    TrasladoSalida,

    /// <summary>Dinero que llega a este fondo desde otro.</summary>
    TrasladoEntrada
}
