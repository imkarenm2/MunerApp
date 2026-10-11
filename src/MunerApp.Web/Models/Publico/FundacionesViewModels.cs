using MunerApp.Domain.Enums;

namespace MunerApp.Web.Models.Publico;

/// <summary>Logo de una fundación: imagen si la tiene, si no, sus iniciales.</summary>
public record LogoEsalModel(string Nombre, string? Url, int Tamano = 64)
{
    private static readonly HashSet<string> Omitir = new(StringComparer.OrdinalIgnoreCase)
    {
        "fundación", "fundacion", "corporación", "corporacion", "asociación", "asociacion",
        "el", "la", "los", "las", "de", "del", "y", "en"
    };

    public string Iniciales
    {
        get
        {
            var palabras = Nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var utiles = palabras.Where(p => !Omitir.Contains(p)).ToArray();
            if (utiles.Length == 0) utiles = palabras;
            return string.Concat(utiles.Take(2).Select(p => char.ToUpperInvariant(p[0])));
        }
    }
}

public class FundacionTarjeta
{
    public string Slug { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string TipoEntidad { get; set; } = string.Empty;
    public string? DescripcionCorta { get; set; }
    public string? Ciudad { get; set; }
    public string? LogoUrl { get; set; }
}

public class DirectorioViewModel
{
    public string? Busqueda { get; set; }
    public IReadOnlyList<FundacionTarjeta> Fundaciones { get; set; } = Array.Empty<FundacionTarjeta>();
    public int Pagina { get; set; } = 1;
    public int TotalPaginas { get; set; } = 1;
    public int Total { get; set; }
}

public record RedPublica(TipoRedSocial Tipo, string Url)
{
    public string Icono => Tipo switch
    {
        TipoRedSocial.Facebook => "bi-facebook",
        TipoRedSocial.Instagram => "bi-instagram",
        _ => "bi-tiktok"
    };
}

public record DocumentoPublico(string Titulo, string Categoria, string? Descripcion, string Url, string Extension, DateTime Fecha, long TamanoBytes)
{
    public string Tamano => TamanoBytes >= 1024 * 1024 ? $"{TamanoBytes / 1024d / 1024d:0.#} MB" : $"{Math.Max(1, TamanoBytes / 1024)} KB";
    public bool EsPdf => Extension == ".pdf";
}

/// <summary>Una opción de "Otras formas de ayudar" (HU-016).</summary>
public record FormaAyuda(string Codigo, string Titulo, string Descripcion, string Icono, string ClaseIcono, string? Url, bool RequiereCuenta, bool Disponible);

public class PerfilPublicoViewModel
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string TipoEntidad { get; set; } = string.Empty;
    public string Nit { get; set; } = string.Empty;
    public string? DescripcionCorta { get; set; }
    public string? Historia { get; set; }
    public string? Mision { get; set; }
    public string? Vision { get; set; }
    public string? Ciudad { get; set; }
    public string? Telefono { get; set; }
    public string Correo { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public DateTime FechaRegistro { get; set; }
    public IReadOnlyList<string> Fotos { get; set; } = Array.Empty<string>();
    public IReadOnlyList<RedPublica> Redes { get; set; } = Array.Empty<RedPublica>();
    public IReadOnlyList<DocumentoPublico> Documentos { get; set; } = Array.Empty<DocumentoPublico>();
    public IReadOnlyList<FormaAyuda> FormasAyuda { get; set; } = Array.Empty<FormaAyuda>();

    /// <summary>Causas de recaudación de la fundación (HU-041 / HU-042).</summary>
    public CausasPublicasViewModel Causas { get; set; } = new();

    /// <summary>Últimas publicaciones del boletín (HU-028).</summary>
    public IReadOnlyList<PublicacionTarjeta> Boletin { get; set; } = Array.Empty<PublicacionTarjeta>();
    public bool TieneDatosDonacion { get; set; }

    /// <summary>true si la fundación tiene algún módulo configurable de apoyo activo.</summary>
    public bool TieneModulosApoyo { get; set; }

    public LogoEsalModel Logo(int tamano) => new(Nombre, LogoUrl, tamano);
}

public class DonarViewModel
{
    public string Slug { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public bool Disponible { get; set; }
    public string? Titular { get; set; }
    public string? DocumentoTitular { get; set; }
    public string? Entidad { get; set; }
    public string? TipoCuenta { get; set; }
    public bool EsLlave { get; set; }
    public string? Numero { get; set; }
    public string? Instrucciones { get; set; }
    public bool PagosEnLineaActivos { get; set; }
}

// ---------------- HU-020: apadrinamiento (solo información pública) ----------------

public class ApadrinableTarjeta
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Edad { get; set; } = string.Empty;
    public string Sexo { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string? FotoUrl { get; set; }
    public string Historia { get; set; } = string.Empty;
    public decimal AporteSugerido { get; set; }
}

public class ApadrinablesViewModel
{
    public string Slug { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public IReadOnlyList<ApadrinableTarjeta> Beneficiarios { get; set; } = Array.Empty<ApadrinableTarjeta>();
}

public class ApadrinableFichaViewModel
{
    public string Slug { get; set; } = string.Empty;
    public string NombreEsal { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public ApadrinableTarjeta Beneficiario { get; set; } = new();

    /// <summary>Si quien mira ya es su padrino activo (HU-021), el apadrinamiento para abrirlo.</summary>
    public int? ApadrinamientoPropioId { get; set; }
}
