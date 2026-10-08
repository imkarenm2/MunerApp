using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace MunerApp.Web.Validacion;

/// <summary>
/// Validación de correo más estricta que [EmailAddress] (observación de pruebas del Sprint 1):
/// exige usuario@dominio.ext con una extensión real y detecta errores de digitación en los
/// proveedores más comunes (p. ej. "gmail.comgfhfhdg" o "gmial.com") para sugerir la corrección.
/// La misma regla se aplica en el navegador con wwwroot/js/validacion.js.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed partial class CorreoAttribute : ValidationAttribute, IClientModelValidator
{
    public const string MensajeFormato = "Escribe un correo válido, por ejemplo nombre@gmail.com.";

    /// <summary>Si es false solo valida el formato (útil en el inicio de sesión).</summary>
    public bool SugerirCorrecciones { get; set; } = true;

    public CorreoAttribute() : base(MensajeFormato) { }

    // Dominios frecuentes en Colombia; si el correo se parece mucho a uno de estos, se sugiere.
    public static readonly string[] DominiosConocidos =
    {
        "gmail.com", "hotmail.com", "hotmail.es", "outlook.com", "outlook.es", "yahoo.com",
        "yahoo.es", "live.com", "icloud.com", "ucundinamarca.edu.co"
    };

    // Dominios reales que se parecen a los anteriores y no deben marcarse como error.
    private static readonly HashSet<string> DominiosLegitimos = new(StringComparer.OrdinalIgnoreCase)
    {
        "mail.com", "gmx.com", "aol.com", "msn.com", "live.co", "yahoo.co", "email.com", "ymail.com"
    };

    // Extensiones reales que empiezan por "com" y no deben confundirse con ".com" + basura.
    private static readonly HashSet<string> ExtensionesCom = new(StringComparer.OrdinalIgnoreCase)
    {
        "com", "community", "company", "computer", "comcast", "comsec"
    };

    [GeneratedRegex(@"^[A-Za-z0-9._%+-]+@([A-Za-z0-9-]+\.)+[A-Za-z]{2,24}$")]
    private static partial Regex Formato();

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string correo || string.IsNullOrWhiteSpace(correo))
            return ValidationResult.Success; // [Required] se encarga del vacío

        var error = Validar(correo.Trim(), SugerirCorrecciones);
        return error is null
            ? ValidationResult.Success
            : new ValidationResult(error, new[] { validationContext.MemberName ?? string.Empty });
    }

    /// <summary>Devuelve null si el correo es válido o el mensaje de error que se le muestra al usuario.</summary>
    public static string? Validar(string correo, bool sugerir = true)
    {
        if (correo.Length > 254 || !Formato().IsMatch(correo) || correo.Contains("..")
            || correo.StartsWith('.') || correo.Contains(".@"))
            return MensajeFormato;

        var arroba = correo.LastIndexOf('@');
        var usuario = correo[..arroba];
        var dominio = correo[(arroba + 1)..].ToLowerInvariant();

        if (dominio.Split('.').Any(p => p.StartsWith('-') || p.EndsWith('-')))
            return MensajeFormato;

        var extension = dominio[(dominio.LastIndexOf('.') + 1)..];
        if (extension.StartsWith("com") && !ExtensionesCom.Contains(extension))
            return Sugerencia(usuario, dominio[..(dominio.LastIndexOf('.') + 1)] + "com", sugerir);

        if (!sugerir || DominiosConocidos.Contains(dominio) || DominiosLegitimos.Contains(dominio))
            return null;

        // "gmail.comgfhfhdg", "hotmail.coom"... empieza por un dominio conocido y le sobra texto
        foreach (var conocido in DominiosConocidos)
            if (dominio.StartsWith(conocido))
                return Sugerencia(usuario, conocido, true);

        // "gmial.com", "hotmal.com", "gmail.co"... a una o dos letras de un dominio conocido
        if (dominio.Length >= 6)
        {
            var parecido = DominiosConocidos
                .Select(c => new { Dominio = c, Distancia = Distancia(dominio, c) })
                .Where(x => x.Distancia is >= 1 and <= 2)
                .OrderBy(x => x.Distancia)
                .FirstOrDefault();
            if (parecido is not null)
                return Sugerencia(usuario, parecido.Dominio, true);
        }

        return null;
    }

    private static string Sugerencia(string usuario, string dominio, bool sugerir) => sugerir
        ? $"Revisa el correo: ¿quisiste decir {usuario}@{dominio}?"
        : MensajeFormato;

    /// <summary>Distancia de Levenshtein entre dos textos cortos.</summary>
    private static int Distancia(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;
        for (var i = 1; i <= a.Length; i++)
            for (var j = 1; j <= b.Length; j++)
            {
                var costo = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + costo);
            }
        return d[a.Length, b.Length];
    }

    public void AddValidation(ClientModelValidationContext context)
    {
        Agregar(context.Attributes, "data-val", "true");
        Agregar(context.Attributes, "data-val-correo", ErrorMessageString);
        Agregar(context.Attributes, "data-val-correo-sugerir", SugerirCorrecciones ? "true" : "false");
        Agregar(context.Attributes, "maxlength", "254");
        Agregar(context.Attributes, "autocomplete", "email");
        Agregar(context.Attributes, "spellcheck", "false");
    }

    private static void Agregar(IDictionary<string, string> atributos, string clave, string valor)
    {
        if (!atributos.ContainsKey(clave)) atributos.Add(clave, valor);
    }
}
