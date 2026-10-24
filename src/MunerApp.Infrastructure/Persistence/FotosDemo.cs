using MunerApp.Application.Interfaces;

namespace MunerApp.Infrastructure.Persistence;

/// <summary>
/// Fotos de bancos de imágenes libres (Unsplash, licencia gratuita) que vienen con la aplicación en wwwroot/img/fotos.
/// Las usan los datos de ejemplo (FundacionesDemo y BoletinDemo) en lugar de ilustraciones: se copian al almacenamiento
/// como si una fundación las hubiera subido. Si se cambia una foto, basta con reemplazar el archivo .webp (y su versión -640 si la tiene).
/// </summary>
internal static class FotosDemo
{
    /// <summary>Copia la foto (nombre sin extensión) al almacenamiento público. Devuelve null si el archivo no existe.</summary>
    public static async Task<string?> GuardarAsync(IAlmacenamientoArchivos archivos, string carpetaFotos, string carpeta, string foto)
    {
        var ruta = Path.Combine(carpetaFotos, foto + ".webp");
        if (!File.Exists(ruta)) return null;
        await using var stream = File.OpenRead(ruta);
        return await archivos.GuardarAsync(stream, carpeta, ".webp", publico: true);
    }

    /// <summary>Las imágenes de la versión anterior de los datos de ejemplo eran ilustraciones .svg generadas.</summary>
    public static bool EsIlustracion(string? ruta) => ruta is not null && ruta.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);
}
