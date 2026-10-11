namespace MunerApp.Web.Validacion;

/// <summary>
/// Validación de datos de personas de Colombia (HU-030): cédula y celular.
/// Las reglas del navegador están en los atributos del modelo; aquí se normaliza y se valida en el servidor.
/// </summary>
public static class ValidadorPersonas
{
    public const string ErrorCedula = "Escribe tu número de cédula sin puntos: entre 6 y 10 dígitos.";
    public const string ErrorCelular = "Escribe un celular de 10 dígitos que empiece por 3, por ejemplo 3001234567.";

    /// <summary>Patrón del navegador: dígitos con puntos o espacios opcionales.</summary>
    public const string PatronCedula = @"^\s*[0-9][0-9.\s]*\s*$";

    /// <summary>Patrón del navegador: 10 dígitos que empiezan por 3, con espacios opcionales.</summary>
    public const string PatronCelular = @"^\s*(\+?57\s*)?3[0-9\s]{9,12}\s*$";

    /// <summary>Cédula solo con dígitos, o null si no es válida (6 a 10 dígitos, sin empezar por 0).</summary>
    public static string? Cedula(string? texto)
    {
        var digitos = ValidadorCuentas.Digitos(texto);
        return digitos.Length is >= 6 and <= 10 && digitos[0] != '0' ? digitos : null;
    }

    /// <summary>Celular de 10 dígitos que empieza por 3 (se acepta con +57), o null si no es válido.</summary>
    public static string? Celular(string? texto)
    {
        var digitos = ValidadorCuentas.Digitos(texto);
        if (digitos.Length == 12 && digitos.StartsWith("57")) digitos = digitos[2..];
        return digitos.Length == 10 && digitos[0] == '3' ? digitos : null;
    }
}
