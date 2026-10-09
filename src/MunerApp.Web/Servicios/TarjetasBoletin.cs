using MunerApp.Application.Interfaces;
using MunerApp.Domain.Entities;
using MunerApp.Web.Models.Publico;

namespace MunerApp.Web.Servicios;

/// <summary>Arma la tarjeta pública de una publicación del boletín (HU-028). La usan el boletín y el perfil de la fundación.</summary>
public static class TarjetasBoletin
{
    /// <summary>La publicación debe venir con <see cref="Publicacion.Esal"/> cargada.</summary>
    public static PublicacionTarjeta Desde(Publicacion p, IAlmacenamientoArchivos archivos) => new()
    {
        Id = p.Id,
        Titulo = p.Titulo,
        Resumen = p.Resumen ?? Recortar(p.Contenido, 180),
        Categoria = p.Categoria,
        ImagenUrl = p.ImagenRuta is null ? null : archivos.UrlPublica(p.ImagenRuta),
        FechaPublicacion = p.FechaPublicacion ?? p.FechaCreacion,
        FechaEvento = p.FechaEvento,
        LugarEvento = p.LugarEvento,
        SlugEsal = p.Esal!.Slug ?? string.Empty,
        NombreEsal = p.Esal.Nombre
    };

    /// <summary>Texto en una sola línea, recortado con puntos suspensivos.</summary>
    public static string Recortar(string texto, int maximo)
    {
        var plano = texto.ReplaceLineEndings(" ");
        return plano.Length <= maximo ? plano : plano[..maximo].TrimEnd() + "…";
    }
}
