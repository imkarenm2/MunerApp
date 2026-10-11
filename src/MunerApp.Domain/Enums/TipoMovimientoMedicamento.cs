namespace MunerApp.Domain.Enums;

/// <summary>Movimiento del inventario clínico (HU-037, escenario 2).</summary>
public enum TipoMovimientoMedicamento
{
    /// <summary>Cantidad con la que se registró el medicamento.</summary>
    Registro,

    /// <summary>Llegó más cantidad (compra o donación).</summary>
    Entrada,

    /// <summary>Se usó o se desechó.</summary>
    Uso
}
