using System.Globalization;

namespace MunerApp.Web.Servicios;

/// <summary>Formatos para mostrar en pantalla (hora de Colombia, pesos y fechas en español).</summary>
public static class Formatos
{
    private static readonly CultureInfo Co = new("es-CO");
    private static readonly TimeZoneInfo Zona = BuscarZona();

    /// <summary>Convierte una fecha guardada en UTC a la hora de Colombia.</summary>
    public static DateTime Local(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zona);

    /// <summary>Convierte una fecha y hora escrita en hora de Colombia a UTC para guardarla (HU-027).</summary>
    public static DateTime AUtc(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Zona);

    /// <summary>$ 50.000</summary>
    public static string Pesos(decimal valor) => valor.ToString("C0", Co);

    /// <summary>3 de octubre de 2026 (para fechas sin hora, como la de una transferencia).</summary>
    public static string Fecha(DateTime fecha) => fecha.ToString("d 'de' MMMM 'de' yyyy", Co);

    /// <summary>3 oct. 2026 (fecha corta de un momento guardado en UTC).</summary>
    public static string FechaCorta(DateTime utc) => Local(utc).ToString("d MMM yyyy", Co);

    /// <summary>3 de octubre de 2026, 4:35 p. m.</summary>
    public static string FechaHora(DateTime utc) => Local(utc).ToString("d 'de' MMMM 'de' yyyy, h:mm tt", Co);

    /// <summary>"hace 5 minutos", "hace 2 días"...</summary>
    public static string Hace(DateTime utc)
    {
        var diferencia = DateTime.UtcNow - DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        if (diferencia.TotalMinutes < 1) return "hace un momento";
        if (diferencia.TotalMinutes < 60) return $"hace {(int)diferencia.TotalMinutes} min";
        if (diferencia.TotalHours < 24) return $"hace {(int)diferencia.TotalHours} h";
        if (diferencia.TotalDays < 2) return "ayer";
        if (diferencia.TotalDays < 30) return $"hace {(int)diferencia.TotalDays} días";
        return FechaCorta(utc);
    }

    /// <summary>"2 años y 3 meses", "8 meses", "Menos de 1 mes" (a partir de la fecha de nacimiento aproximada).</summary>
    public static string Edad(DateTime nacimiento)
    {
        var hoy = DateTime.Today;
        var meses = (hoy.Year - nacimiento.Year) * 12 + hoy.Month - nacimiento.Month;
        if (hoy.Day < nacimiento.Day) meses--;
        if (meses < 1) return "Menos de 1 mes";
        var anios = meses / 12;
        var resto = meses % 12;
        var a = anios == 1 ? "1 año" : $"{anios} años";
        var m = resto == 1 ? "1 mes" : $"{resto} meses";
        if (anios == 0) return m;
        return resto == 0 ? a : $"{a} y {m}";
    }

    /// <summary>Años y meses de una fecha de nacimiento aproximada (para llenar el formulario al editar).</summary>
    public static (int Anios, int Meses) AniosYMeses(DateTime nacimiento)
    {
        var hoy = DateTime.Today;
        var meses = Math.Max(0, (hoy.Year - nacimiento.Year) * 12 + hoy.Month - nacimiento.Month - (hoy.Day < nacimiento.Day ? 1 : 0));
        return (meses / 12, meses % 12);
    }

    /// <summary>Acepta "50000", "50.000", "$ 50.000" o "50,000" (pesos sin decimales). Devuelve null si no es un valor válido.</summary>
    public static decimal? LeerPesos(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var limpio = texto.Replace("$", "").Replace("COP", "", StringComparison.OrdinalIgnoreCase).Trim();
        if (limpio.Any(c => !char.IsDigit(c) && c is not '.' and not ',' and not ' ')) return null;
        var digitos = new string(limpio.Where(char.IsDigit).ToArray());
        return digitos.Length is > 0 and <= 12 ? decimal.Parse(digitos) : null;
    }

    /// <summary>Porcentaje de la meta alcanzado (0 a 100). Si hay algo recaudado nunca muestra 0 %.</summary>
    public static int Porcentaje(decimal recaudado, decimal meta)
    {
        if (meta <= 0 || recaudado <= 0) return 0;
        var p = (int)Math.Floor(recaudado * 100 / meta);
        return Math.Clamp(p == 0 ? 1 : p, 0, 100);
    }

    /// <summary>"Último día", "1 día restante" o "12 días restantes".</summary>
    public static string DiasRestantes(int dias) => dias switch
    {
        <= 0 => "Último día",
        1 => "1 día restante",
        _ => $"{dias} días restantes"
    };

    /// <summary>Enlace de WhatsApp para un número colombiano.</summary>
    public static string? WhatsApp(string? telefono)
    {
        if (string.IsNullOrWhiteSpace(telefono)) return null;
        var digitos = new string(telefono.Where(char.IsDigit).ToArray());
        if (digitos.Length == 10 && digitos.StartsWith('3')) digitos = "57" + digitos;
        return digitos.Length >= 11 ? $"https://wa.me/{digitos}" : null;
    }

    private static TimeZoneInfo BuscarZona()
    {
        foreach (var id in new[] { "America/Bogota", "SA Pacific Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("COT", TimeSpan.FromHours(-5), "Colombia", "Colombia");
    }
}
