using System.Globalization;
using System.Text;

namespace MunerApp.Domain.Common;

/// <summary>Convierte un nombre en una dirección amigable: "El Reino de los Gatos" → "el-reino-de-los-gatos".</summary>
public static class Slugs
{
    public static string Generar(string texto, int maximo = 80)
    {
        var normalizado = (texto ?? string.Empty).Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        var guion = false;
        foreach (var c in normalizado)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            var ch = char.ToLowerInvariant(c);
            if (ch is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                sb.Append(ch);
                guion = false;
            }
            else if (!guion && sb.Length > 0)
            {
                sb.Append('-');
                guion = true;
            }
        }
        var slug = sb.ToString().Trim('-');
        if (slug.Length > maximo) slug = slug[..maximo].Trim('-');
        return string.IsNullOrEmpty(slug) ? "fundacion" : slug;
    }
}
