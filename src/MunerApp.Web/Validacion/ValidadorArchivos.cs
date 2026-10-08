namespace MunerApp.Web.Validacion;

public enum TipoArchivo
{
    /// <summary>Logo y fotos: JPG, PNG o WEBP hasta 5 MB.</summary>
    Imagen,

    /// <summary>Documentos y soportes: PDF, JPG o PNG hasta 5 MB.</summary>
    Documento
}

public record ArchivoValidado(bool Valido, string? Error, string Extension, string ContentType)
{
    public static ArchivoValidado Falla(string error) => new(false, error, string.Empty, string.Empty);
}

/// <summary>
/// Valida los archivos que suben los usuarios (Sprint 2). Además de la extensión y el tamaño,
/// revisa la firma real del archivo (sus primeros bytes), para que un .exe renombrado a .pdf no pase.
/// </summary>
public static class ValidadorArchivos
{
    public const long MaxImagenBytes = 5 * 1024 * 1024; // las fotos de celular suelen pesar entre 2 y 5 MB
    public const long MaxDocumentoBytes = 5 * 1024 * 1024;

    /// <summary>Valor para el atributo accept de los input file.</summary>
    public static string Accept(TipoArchivo tipo) => tipo == TipoArchivo.Imagen
        ? ".jpg,.jpeg,.png,.webp,image/jpeg,image/png,image/webp"
        : ".pdf,.jpg,.jpeg,.png,application/pdf,image/jpeg,image/png";

    /// <summary>Límite de cada petición con archivos (un poco más que el archivo, por los demás campos).</summary>
    public const long LimitePeticionBytes = 12 * 1024 * 1024;

    public static string Ayuda(TipoArchivo tipo) => tipo == TipoArchivo.Imagen
        ? "JPG, PNG o WEBP de máximo 5 MB."
        : "PDF, JPG o PNG de máximo 5 MB.";

    public static async Task<ArchivoValidado> ValidarAsync(IFormFile? archivo, TipoArchivo tipo)
    {
        if (archivo is null || archivo.Length == 0)
            return ArchivoValidado.Falla("Selecciona un archivo.");

        var max = tipo == TipoArchivo.Imagen ? MaxImagenBytes : MaxDocumentoBytes;
        if (archivo.Length > max)
            return ArchivoValidado.Falla($"El archivo pesa {archivo.Length / 1024d / 1024d:0.#} MB. Formatos permitidos: {Ayuda(tipo)}");

        var cabecera = new byte[12];
        await using (var stream = archivo.OpenReadStream())
        {
            var leidos = await stream.ReadAsync(cabecera.AsMemory(0, cabecera.Length));
            if (leidos < 4) return ArchivoValidado.Falla($"El archivo está vacío o dañado. Formatos permitidos: {Ayuda(tipo)}");
        }

        var detectado = Detectar(cabecera);
        var extensionNombre = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (extensionNombre == ".jpeg") extensionNombre = ".jpg";

        var permitidos = tipo == TipoArchivo.Imagen ? new[] { ".jpg", ".png", ".webp" } : new[] { ".pdf", ".jpg", ".png" };
        if (detectado is null || !permitidos.Contains(detectado.Value.Extension) || extensionNombre != detectado.Value.Extension)
            return ArchivoValidado.Falla($"El archivo no es válido. Formatos permitidos: {Ayuda(tipo)}");

        return new ArchivoValidado(true, null, detectado.Value.Extension, detectado.Value.ContentType);
    }

    public static string ContentTypeDe(string clave) => Path.GetExtension(clave).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "image/jpeg"
    };

    private static (string Extension, string ContentType)? Detectar(byte[] b)
    {
        if (b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return (".jpg", "image/jpeg");
        if (b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return (".png", "image/png");
        if (b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46) return (".pdf", "application/pdf"); // %PDF
        if (b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46
            && b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50) return (".webp", "image/webp"); // RIFF....WEBP
        return null;
    }
}
