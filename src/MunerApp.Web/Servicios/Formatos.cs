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
