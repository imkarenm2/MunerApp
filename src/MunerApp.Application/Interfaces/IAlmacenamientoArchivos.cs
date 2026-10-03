namespace MunerApp.Application.Interfaces;

/// <summary>
/// Guarda los archivos que suben los usuarios (Sprint 2).
/// Públicos: logo, fotos y documentos de transparencia; se sirven en /archivos/...
/// Privados: soportes de pago y académicos; solo se entregan por un controlador que valida permisos.
/// </summary>
public interface IAlmacenamientoArchivos
{
    /// <summary>Guarda el archivo y devuelve su clave (ruta relativa) para guardarla en la base de datos.</summary>
    Task<string> GuardarAsync(Stream contenido, string carpeta, string extension, bool publico, CancellationToken ct = default);

    /// <summary>Abre un archivo para leerlo. Devuelve null si no existe.</summary>
    Task<Stream?> AbrirAsync(string clave, CancellationToken ct = default);

    Task EliminarAsync(string clave, CancellationToken ct = default);

    /// <summary>URL para mostrar un archivo público en una página, por ejemplo /archivos/esal/3/logo/abc.webp.</summary>
    string UrlPublica(string clave);
}
