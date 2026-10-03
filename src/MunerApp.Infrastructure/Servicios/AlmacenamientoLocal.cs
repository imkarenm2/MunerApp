using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MunerApp.Application.Interfaces;

namespace MunerApp.Infrastructure.Servicios;

/// <summary>
/// Almacenamiento en disco. En desarrollo usa la carpeta App_Data del proyecto web; en Azure App Service
/// se configura Archivos:RutaBase = /home/data/archivos (D:\home\data\archivos en Windows), que es
/// almacenamiento persistente y compartido. Si más adelante se usa Azure Blob Storage, basta con otra
/// implementación de IAlmacenamientoArchivos.
/// </summary>
public class AlmacenamientoLocal : IAlmacenamientoArchivos
{
    public const string PrefijoPublico = "pub/";
    public const string PrefijoPrivado = "priv/";
    public const string RutaUrlPublica = "/archivos";

    private readonly string _base;

    public AlmacenamientoLocal(IConfiguration config, IHostEnvironment entorno)
    {
        _base = RutaBase(config, entorno);
    }

    /// <summary>Carpeta raíz de los archivos (Program.cs la usa para servir los públicos).</summary>
    public static string RutaBase(IConfiguration config, IHostEnvironment entorno)
    {
        var configurada = config["Archivos:RutaBase"];
        var ruta = string.IsNullOrWhiteSpace(configurada)
            ? Path.Combine(entorno.ContentRootPath, "App_Data", "archivos")
            : configurada;
        return Path.GetFullPath(ruta);
    }

    public static string RutaPublica(IConfiguration config, IHostEnvironment entorno)
        => Path.Combine(RutaBase(config, entorno), "pub");

    public async Task<string> GuardarAsync(Stream contenido, string carpeta, string extension, bool publico, CancellationToken ct = default)
    {
        carpeta = carpeta.Trim('/').Replace('\\', '/');
        extension = extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();

        // El nombre lo genera la plataforma: nunca se usa el nombre que envía el usuario
        var clave = $"{(publico ? PrefijoPublico : PrefijoPrivado)}{carpeta}/{Guid.NewGuid():N}{extension}";
        var ruta = RutaFisica(clave);
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);

        await using var destino = new FileStream(ruta, FileMode.CreateNew, FileAccess.Write);
        await contenido.CopyToAsync(destino, ct);
        return clave;
    }

    public Task<Stream?> AbrirAsync(string clave, CancellationToken ct = default)
    {
        var ruta = RutaFisica(clave);
        Stream? stream = File.Exists(ruta) ? new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
        return Task.FromResult(stream);
    }

    public Task EliminarAsync(string clave, CancellationToken ct = default)
    {
        var ruta = RutaFisica(clave);
        if (File.Exists(ruta)) File.Delete(ruta);
        return Task.CompletedTask;
    }

    public string UrlPublica(string clave)
    {
        if (!clave.StartsWith(PrefijoPublico))
            throw new InvalidOperationException("Solo los archivos públicos tienen URL directa.");
        return $"{RutaUrlPublica}/{clave[PrefijoPublico.Length..]}";
    }

    /// <summary>Convierte la clave en ruta del disco, impidiendo salir de la carpeta base (../).</summary>
    private string RutaFisica(string clave)
    {
        if (string.IsNullOrWhiteSpace(clave) || clave.Contains("..") || Path.IsPathRooted(clave))
            throw new ArgumentException("Clave de archivo inválida.", nameof(clave));

        var ruta = Path.GetFullPath(Path.Combine(_base, clave.Replace('/', Path.DirectorySeparatorChar)));
        if (!ruta.StartsWith(_base + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("Clave de archivo inválida.", nameof(clave));
        return ruta;
    }
}
