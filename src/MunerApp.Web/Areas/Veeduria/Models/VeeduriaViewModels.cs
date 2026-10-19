using MunerApp.Domain.Enums;

namespace MunerApp.Web.Areas.Veeduria.Models;

/// <summary>Listado de fundaciones que vigila la veeduría, con un resumen del último año.</summary>
public class FundacionVeeduriaItem
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Ciudad { get; set; }
    public string Nit { get; set; } = string.Empty;
    public decimal DineroConfirmado { get; set; }
    public int DonacionesDinero { get; set; }
    public int DonacionesEspecie { get; set; }
    public int CausasCerradasSinRendicion { get; set; }
    public int DonacionesSinRevisar { get; set; }
    public int DocumentosPublicados { get; set; }
}

public class FundacionVeeduriaViewModel
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Nit { get; set; } = string.Empty;
    public string? Ciudad { get; set; }
    public string? Slug { get; set; }
    public int Anio { get; set; }
    public IReadOnlyList<int> Anios { get; set; } = Array.Empty<int>();

    public decimal DineroConfirmado { get; set; }
    public int DonacionesDinero { get; set; }
    public int DonacionesPendientes { get; set; }
    public int PendientesMasDeUnaSemana { get; set; }
    public double? DiasPromedioRevision { get; set; }
    public int EspecieRecibidas { get; set; }
    public int EspecieNoRecibidas { get; set; }

    public IReadOnlyList<(string Destino, decimal Valor, int Cantidad)> PorDestino { get; set; } = Array.Empty<(string, decimal, int)>();
    public IReadOnlyList<CausaVeeduria> Causas { get; set; } = Array.Empty<CausaVeeduria>();
    public IReadOnlyList<DonacionVeeduria> Dinero { get; set; } = Array.Empty<DonacionVeeduria>();
    public IReadOnlyList<EspecieVeeduria> Especie { get; set; } = Array.Empty<EspecieVeeduria>();
    public IReadOnlyList<DocumentoVeeduria> Documentos { get; set; } = Array.Empty<DocumentoVeeduria>();
}

public record CausaVeeduria(int Id, string Titulo, EstadoCausa Estado, bool Cerrada, decimal Meta, decimal Recaudado, DateTime FechaLimite, string? RendicionUrl);
public record DonacionVeeduria(string Codigo, DateTime Fecha, decimal Valor, string Destino, string Medio);
public record EspecieVeeduria(string Codigo, DateTime Fecha, string Articulo, string Categoria, int Cantidad, string Unidad, DateTime? Vence, string? FotoUrl);
public record DocumentoVeeduria(string Titulo, string Categoria, DateTime Fecha, string Url);
