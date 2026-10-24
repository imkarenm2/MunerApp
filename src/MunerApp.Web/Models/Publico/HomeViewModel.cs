namespace MunerApp.Web.Models.Publico;

/// <summary>Inicio público: cifras reales de la plataforma, causas, beneficiarios que buscan padrino y la región donde estamos.</summary>
public class HomeViewModel
{
    public IReadOnlyList<FundacionTarjeta> Fundaciones { get; set; } = Array.Empty<FundacionTarjeta>();
    public IReadOnlyList<CausaTarjeta> Causas { get; set; } = Array.Empty<CausaTarjeta>();
    public IReadOnlyList<ApadrinableHome> Apadrinables { get; set; } = Array.Empty<ApadrinableHome>();
    public IReadOnlyList<MunicipioHome> Municipios { get; set; } = Array.Empty<MunicipioHome>();

    // Cifras públicas: solo lo que las fundaciones ya publican
    public int FundacionesPublicadas { get; set; }
    /// <summary>Recaudado y confirmado en causas públicas (lo mismo que suman sus barras de progreso).</summary>
    public decimal RecaudadoCausas { get; set; }

    /// <summary>Rendiciones de cuentas publicadas por las fundaciones.</summary>
    public int Rendiciones { get; set; }
    public int CausasAbiertas { get; set; }
    public int Padrinos { get; set; }

    /// <summary>Resumen para el donante que ya inició sesión (null si es un visitante).</summary>
    public ResumenDonanteHome? Donante { get; set; }
}

public record ApadrinableHome(int Id, string Nombre, string? FotoUrl, string Fundacion, string Slug, string? Historia);

public record MunicipioHome(string Nombre, int Fundaciones);

public record ResumenDonanteHome(string Nombre, int DonacionesConfirmadas, decimal TotalDonado, int DonacionesPendientes, int Apadrinamientos);
