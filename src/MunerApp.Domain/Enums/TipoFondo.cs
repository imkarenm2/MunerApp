namespace MunerApp.Domain.Enums;

/// <summary>
/// Libro de fondos: a dónde se asigna cada peso que entra. El dinero se sigue por fondo, no peso por peso.
/// </summary>
public enum TipoFondo
{
    /// <summary>Donaciones sin destino: la fundación decide en qué se usan. Uno por fundación.</summary>
    General,

    /// <summary>Una causa de recaudación. Recibe las donaciones hechas a esa causa.</summary>
    Causa,

    /// <summary>Aportes de apadrinamiento. Uno por fundación.</summary>
    Apadrinamiento,

    /// <summary>Proyecto que crea la fundación (por ejemplo, "Jornada de esterilización"). Se llena con traslados.</summary>
    Proyecto
}
