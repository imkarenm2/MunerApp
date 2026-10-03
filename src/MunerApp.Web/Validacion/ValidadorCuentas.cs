using System.Text.RegularExpressions;
using MunerApp.Domain.Enums;

namespace MunerApp.Web.Validacion;

/// <summary>
/// Reglas de los datos para donar (observación 7 de pruebas del Sprint 2).
/// - Cuentas de ahorros y corrientes: solo números (6 a 20 dígitos).
/// - Billeteras digitales (Nequi, Daviplata...): el número es un celular colombiano de 10 dígitos que empieza por 3.
/// - Llaves Bre-B: según su tipo. La única que admite letras es la alfanumérica, que empieza con @.
///   La llave de tipo NIT debe ser el NIT de la fundación, porque la cuenta debe estar a su nombre.
/// No es posible comprobar desde aquí que la cuenta o la llave existan: los bancos no ofrecen esa consulta a terceros.
/// </summary>
public static partial class ValidadorCuentas
{
    [GeneratedRegex(@"^3[0-9]{9}$")]
    private static partial Regex Celular();

    [GeneratedRegex(@"^[0-9]{6,20}$")]
    private static partial Regex NumeroCuenta();

    [GeneratedRegex(@"^@[A-Za-z0-9ÁÉÍÓÚÜÑáéíóúüñ]{3,20}$")]
    private static partial Regex LlaveAlfanumerica();

    [GeneratedRegex(@"^[A-Za-z0-9ÁÉÍÓÚÜÑáéíóúüñ .&!-]{2,100}$")]
    private static partial Regex Entidad();

    /// <summary>Solo los dígitos de un texto ("300 123 4567" → "3001234567").</summary>
    public static string Digitos(string? texto) => new((texto ?? string.Empty).Where(char.IsDigit).ToArray());

    /// <summary>NIT sin dígito de verificación ("900123456-7" → "900123456").</summary>
    public static string NitBase(string? nit) => Digitos((nit ?? string.Empty).Split('-')[0]);

    public static bool EntidadValida(string? entidad) => entidad is not null && Entidad().IsMatch(entidad.Trim());

    /// <summary>Valida y normaliza el número o la llave. Devuelve (valor normalizado, error).</summary>
    public static (string Valor, string? Error) Validar(TipoCuentaDonacion tipo, TipoLlave? llave, string? numero, string nitEsal)
    {
        var texto = (numero ?? string.Empty).Trim();
        if (texto.Length == 0) return (texto, "Ingresa la llave o el número de cuenta.");

        switch (tipo)
        {
            case TipoCuentaDonacion.Ahorros:
            case TipoCuentaDonacion.Corriente:
                if (texto.Any(char.IsLetter)) return (texto, "El número de cuenta solo puede tener números.");
                var cuenta = Digitos(texto);
                return NumeroCuenta().IsMatch(cuenta)
                    ? (cuenta, null)
                    : (texto, "El número de cuenta debe tener entre 6 y 20 dígitos.");

            case TipoCuentaDonacion.BilleteraDigital:
                if (texto.Any(char.IsLetter)) return (texto, "En billeteras digitales el número es el celular: solo números.");
                var cel = Digitos(texto);
                return Celular().IsMatch(cel)
                    ? (cel, null)
                    : (texto, "Escribe el celular de la billetera: 10 dígitos que empiezan por 3, por ejemplo 3001234567.");

            case TipoCuentaDonacion.Llave:
                switch (llave)
                {
                    case TipoLlave.Documento:
                        if (texto.Any(char.IsLetter)) return (texto, "La llave de NIT solo puede tener números.");
                        var nit = NitBase(texto);
                        if (nit != NitBase(nitEsal))
                            return (texto, $"La llave de NIT debe ser el NIT de la fundación ({NitBase(nitEsal)}), sin dígito de verificación.");
                        return (nit, null);

                    case TipoLlave.Celular:
                        if (texto.Any(char.IsLetter)) return (texto, "La llave de celular solo puede tener números.");
                        var llaveCel = Digitos(texto);
                        if (llaveCel.Length == 12 && llaveCel.StartsWith("57")) llaveCel = llaveCel[2..];
                        return Celular().IsMatch(llaveCel)
                            ? (llaveCel, null)
                            : (texto, "La llave de celular debe tener 10 dígitos y empezar por 3, por ejemplo 3001234567.");

                    case TipoLlave.Correo:
                        var correo = texto.ToLowerInvariant();
                        var error = CorreoAttribute.Validar(correo);
                        return error is null ? (correo, null) : (texto, "La llave de correo no es válida. " + error);

                    case TipoLlave.Alfanumerica:
                        if (!texto.StartsWith('@')) texto = "@" + texto;
                        return LlaveAlfanumerica().IsMatch(texto)
                            ? (texto, null)
                            : (texto, "La llave alfanumérica empieza con @ y lleva solo letras y números, sin espacios ni símbolos (entre 3 y 20 caracteres), por ejemplo @reinogatos.");

                    default:
                        return (texto, "Selecciona el tipo de llave.");
                }

            default:
                return (texto, "Selecciona el tipo de cuenta.");
        }
    }
}
