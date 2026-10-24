using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;

namespace MunerApp.Infrastructure.Persistence;

/// <summary>Lo que comparten los datos de ejemplo (FundacionesDemo y BoletinDemo).</summary>
internal static class DatosDemo
{
    /// <summary>Colombia es UTC-5 todo el año (no tiene horario de verano).</summary>
    public static readonly TimeSpan DiferenciaColombia = TimeSpan.FromHours(5);

    public static DateTime HoyColombia() => (DateTime.UtcNow - DiferenciaColombia).Date;

    /// <summary>Id de un módulo por su código (CodigosModulo), sin depender del número con que se sembró.</summary>
    public static Task<int> ModuloIdAsync(MunerAppDbContext db, string codigo)
        => db.Modulos.Where(m => m.Codigo == codigo).Select(m => m.Id).FirstAsync();

    /// <summary>Cambia una imagen guardada por una foto de ejemplo y borra el archivo anterior del almacenamiento.</summary>
    public static async Task<string> ReemplazarAsync(IAlmacenamientoArchivos archivos, string carpetaFotos,
        string rutaAnterior, string carpeta, string foto)
    {
        var nueva = await FotosDemo.GuardarAsync(archivos, carpetaFotos, carpeta, foto);
        if (nueva is null) return rutaAnterior;
        await archivos.EliminarAsync(rutaAnterior);
        return nueva;
    }
}
