using MunerApp.Domain.Enums;

namespace MunerApp.Domain.Constantes;

/// <summary>
/// A qué estados puede pasar un beneficiario desde su estado actual (HU-017, escenario 2).
/// "Fallecido" es final. Adoptado, liberado o "encontró su hogar" solo pueden volver a la fundación
/// (por ejemplo, una devolución) o registrarse como fallecido.
/// </summary>
public static class ReglasBeneficiario
{
    private static readonly EstadoBeneficiario[] Salidas =
        { EstadoBeneficiario.Adoptado, EstadoBeneficiario.Liberado, EstadoBeneficiario.EncontroSuHogar, EstadoBeneficiario.Fallecido };

    private static readonly Dictionary<EstadoBeneficiario, EstadoBeneficiario[]> Permitidos = new()
    {
        [EstadoBeneficiario.EnLaFundacion] = new[] { EstadoBeneficiario.EnTratamiento, EstadoBeneficiario.Adoptable }.Concat(Salidas).ToArray(),
        [EstadoBeneficiario.EnTratamiento] = new[] { EstadoBeneficiario.EnLaFundacion, EstadoBeneficiario.Adoptable, EstadoBeneficiario.Fallecido },
        [EstadoBeneficiario.Adoptable] = new[] { EstadoBeneficiario.EnLaFundacion, EstadoBeneficiario.EnTratamiento }.Concat(Salidas).ToArray(),
        // Si un adoptado, liberado o reubicado regresa, vuelve a "En la fundación"
        [EstadoBeneficiario.Adoptado] = new[] { EstadoBeneficiario.EnLaFundacion, EstadoBeneficiario.Fallecido },
        [EstadoBeneficiario.Liberado] = new[] { EstadoBeneficiario.EnLaFundacion, EstadoBeneficiario.Fallecido },
        [EstadoBeneficiario.EncontroSuHogar] = new[] { EstadoBeneficiario.EnLaFundacion, EstadoBeneficiario.Fallecido },
        [EstadoBeneficiario.Fallecido] = Array.Empty<EstadoBeneficiario>()
    };

    public static IReadOnlyList<EstadoBeneficiario> Siguientes(EstadoBeneficiario actual) =>
        Permitidos.TryGetValue(actual, out var estados) ? estados : Array.Empty<EstadoBeneficiario>();

    public static bool PuedeCambiar(EstadoBeneficiario actual, EstadoBeneficiario nuevo) => Siguientes(actual).Contains(nuevo);

    /// <summary>Estados en los que el beneficiario ya no está en la fundación: se cierran sus apadrinamientos.</summary>
    public static bool TerminaApadrinamientos(EstadoBeneficiario estado) => Salidas.Contains(estado);

    /// <summary>Fin de reseña: el beneficiario ya no está a cargo de la fundación.</summary>
    public static bool EsSalida(EstadoBeneficiario estado) => Salidas.Contains(estado);

    /// <summary>Estados de los gatos que siguen bajo el cuidado de la fundación (los que no han salido).</summary>
    public static readonly EstadoBeneficiario[] EnCasa =
        { EstadoBeneficiario.EnLaFundacion, EstadoBeneficiario.EnTratamiento, EstadoBeneficiario.Adoptable };

    /// <summary>Días después de los cuales la vacuna o la desparasitación se consideran vencidas (tablero del panel).</summary>
    public const int DiasVacuna = 365;
    public const int DiasDesparasitacion = 90;
}
