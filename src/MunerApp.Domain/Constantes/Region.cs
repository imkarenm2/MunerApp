namespace MunerApp.Domain.Constantes;

/// <summary>
/// Por ahora MunerApp funciona solo en la provincia de Sabana de Occidente (Cundinamarca).
/// Las fundaciones eligen su municipio de esta lista.
/// </summary>
public static class Region
{
    public const string Nombre = "Sabana de Occidente";

    public static readonly string[] Municipios =
        { "Bojacá", "El Rosal", "Facatativá", "Funza", "Madrid", "Mosquera", "Subachoque", "Zipacón" };

    public static bool EsMunicipioValido(string? municipio)
        => !string.IsNullOrWhiteSpace(municipio) && Municipios.Contains(municipio.Trim());
}
