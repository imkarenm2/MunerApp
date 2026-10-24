namespace MunerApp.Application.Exportacion;

/// <summary>Celdas de los archivos CSV que se descargan y se abren en Excel (veeduría).</summary>
public static class ArchivoCsv
{
    /// <summary>
    /// Texto entre comillas y con las comillas internas duplicadas. Si empieza por = + - @ (o tabulador / salto),
    /// Excel lo ejecutaría como fórmula: se antepone ' para que quede como texto (inyección de fórmulas).
    /// </summary>
    public static string Celda(string? valor)
    {
        var texto = valor ?? "";
        if (texto.Length > 0 && "=+-@\t\r".Contains(texto[0])) texto = "'" + texto;
        return "\"" + texto.Replace("\"", "\"\"") + "\"";
    }
}
