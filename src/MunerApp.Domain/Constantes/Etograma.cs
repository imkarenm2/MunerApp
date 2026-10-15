namespace MunerApp.Domain.Constantes;

/// <summary>Conductas del etograma, tal como aparecen en el formato de la fundación.</summary>
public static class Etograma
{
    public record Conducta(string Codigo, string Grupo, string Texto, bool Deseable);

    public const string Sociales = "Conductas sociales";
    public const string Miedos = "Miedos";
    public const string NoDeseables = "Conductas no deseables";

    public static readonly Conducta[] Todas =
    {
        new("S1", Sociales, "Come, bebe y duerme adecuadamente", true),
        new("S2", Sociales, "Es manipulable en su totalidad", true),
        new("S3", Sociales, "Orina y defeca con normalidad", true),
        new("S4", Sociales, "Juega con normalidad", true),
        new("S5", Sociales, "Se relaja con normalidad", true),
        new("S6", Sociales, "Se relaciona normalmente con el entorno", true),

        new("M1", Miedos, "Miedo cuando le ofrecen alimento", false),
        new("M2", Miedos, "Siente seguridad cuando es tocado", true),
        new("M3", Miedos, "Dedica mucho tiempo a ocultar sus deposiciones", false),
        new("M4", Miedos, "Se esconde al contacto con otros gatos", false),
        new("M5", Miedos, "Solamente se relaja en su ambiente", false),
        new("M6", Miedos, "No es curioso, siempre tiene miedo", false),

        new("N1", NoDeseables, "Protección de recursos con la comida", false),
        new("N2", NoDeseables, "Gruñe o muerde si se manipula", false),
        new("N3", NoDeseables, "Hace las heces u orina fuera de su arenero", false),
        new("N4", NoDeseables, "Se muestra agresivo, se esconde", false),
        new("N5", NoDeseables, "Rasguña y muerde ante una molestia", false),
        new("N6", NoDeseables, "Reacciona mal ante personas u otros animales", false)
    };

    public static readonly string[] Grupos = { Sociales, Miedos, NoDeseables };

    public static IEnumerable<Conducta> De(string? codigos) =>
        (codigos ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(c => Todas.FirstOrDefault(x => x.Codigo == c.Trim()))
            .Where(c => c is not null)!;

    /// <summary>Vacunas del formato de la fundación.</summary>
    public static readonly string[] Vacunas = { "Triple felina", "Rabia", "Leucemia felina" };
}
