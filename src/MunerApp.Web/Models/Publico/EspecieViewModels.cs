using System.ComponentModel.DataAnnotations;
using MunerApp.Domain.Enums;

namespace MunerApp.Web.Models.Publico;

/// <summary>Página pública de donación en especie de una fundación: lo que necesita y cuánto le falta.</summary>
public class DonarEspecieViewModel
{
    public string Slug { get; set; } = string.Empty;
    public string NombreEsal { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? Direccion { get; set; }
    public IReadOnlyList<NecesidadPublica> Necesidades { get; set; } = Array.Empty<NecesidadPublica>();

    /// <summary>Lo recibido en especie en los últimos 12 meses, por tipo de artículo (transparencia).</summary>
    public IReadOnlyList<RecibidoPorCategoria> Recibido { get; set; } = Array.Empty<RecibidoPorCategoria>();
}

public record NecesidadPublica(int Id, string Nombre, CategoriaEspecie Categoria, string Unidad, int Meta, int Recibido, string? Descripcion, bool Urgente)
{
    public int Porcentaje => Meta <= 0 ? 0 : Math.Min(100, (int)Math.Round(100.0 * Recibido / Meta));
    public int Faltan => Math.Max(0, Meta - Recibido);
}

public record RecibidoPorCategoria(CategoriaEspecie Categoria, int Donaciones, string Detalle);

public class OfrecerEspecieViewModel
{
    public string Slug { get; set; } = string.Empty;
    public string NombreEsal { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }

    /// <summary>Necesidad a la que responde (null si ofrece otra cosa).</summary>
    public int? NecesidadId { get; set; }
    public string? NombreNecesidad { get; set; }

    [Required(ErrorMessage = "Escribe qué vas a donar.")]
    [StringLength(120, MinimumLength = 3, ErrorMessage = "Entre {2} y {1} caracteres.")]
    [Display(Name = "¿Qué vas a donar?")]
    public string Articulo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona el tipo.")]
    [Display(Name = "Tipo")]
    public CategoriaEspecie? Categoria { get; set; }

    [Required(ErrorMessage = "Escribe la cantidad.")]
    [Range(1, 10000, ErrorMessage = "Escribe un número entre {1} y {2}.")]
    [Display(Name = "Cantidad")]
    public int? Cantidad { get; set; }

    [Required(ErrorMessage = "Escribe la unidad, por ejemplo bultos o unidades.")]
    [StringLength(40, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Unidad")]
    public string Unidad { get; set; } = string.Empty;

    [Required(ErrorMessage = "Elige cómo la entregas.")]
    [Display(Name = "¿Cómo llega a la fundación?")]
    public FormaEntregaEspecie? FormaEntrega { get; set; }

    [Required(ErrorMessage = "Elige la fecha.")]
    [DataType(DataType.Date)]
    [Display(Name = "¿Qué día la entregas?")]
    public DateTime? FechaEntrega { get; set; }

    [StringLength(200, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Dirección para recogerla")]
    public string? DireccionRecogida { get; set; }

    [StringLength(300, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Mensaje para la fundación (opcional)")]
    public string? Mensaje { get; set; }
}

/// <summary>Una donación en especie en "Mis donaciones".</summary>
public class MiDonacionEspecieItem
{
    public string Codigo { get; set; } = string.Empty;
    public string NombreEsal { get; set; } = string.Empty;
    public string? SlugEsal { get; set; }
    public string? LogoUrl { get; set; }
    public string Articulo { get; set; } = string.Empty;
    public CategoriaEspecie Categoria { get; set; }
    public int CantidadOfrecida { get; set; }
    public int? CantidadRecibida { get; set; }
    public string Unidad { get; set; } = string.Empty;
    public FormaEntregaEspecie FormaEntrega { get; set; }
    public DateTime FechaEntregaEstimada { get; set; }
    public DateTime FechaOferta { get; set; }
    public EstadoDonacionEspecie Estado { get; set; }
    public DateTime? FechaRecepcion { get; set; }
    public string? MotivoNoRecibida { get; set; }
    public string? FotoUrl { get; set; }
    public string? DireccionEsal { get; set; }

    /// <summary>Qué hizo la fundación con lo recibido (destinación del lote).</summary>
    public IReadOnlyList<UsoEspeciePublico> Usos { get; set; } = Array.Empty<UsoEspeciePublico>();
    public int Usado => Usos.Where(u => !u.Anulado).Sum(u => u.Cantidad);
}

/// <summary>Uso de una donación en especie, como lo ven el donante y la veeduría (sin datos de quién lo registró).</summary>
public record UsoEspeciePublico(int Cantidad, string Uso, DateTime Fecha, string? FotoUrl, bool Anulado, string? MotivoAnulacion);
