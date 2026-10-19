namespace MunerApp.Web.Models.Publico;

/// <summary>Inicio público: cifras reales de la plataforma, causas, gatos que buscan padrino y la región donde estamos.</summary>
public class HomeViewModel
{
    public IReadOnlyList<FundacionTarjeta> Fundaciones { get; set; } = Array.Empty<FundacionTarjeta>();
    public IReadOnlyList<CausaTarjeta> Causas { get; set; } = Array.Empty<CausaTarjeta>();
    public IReadOnlyList<GatoApadrinableHome> Gatos { get; set; } = Array.Empty<GatoApadrinableHome>();
    public IReadOnlyList<string> FotosHero { get; set; } = Array.Empty<string>();
    public IReadOnlyList<MunicipioHome> Municipios { get; set; } = Array.Empty<MunicipioHome>();

    // Cifras reales
    public int FundacionesPublicadas { get; set; }
    public int GatosConHogar { get; set; }
    public int GatosCuidados { get; set; }
    public decimal Donado { get; set; }
    public int CausasAbiertas { get; set; }
    public int Padrinos { get; set; }

    /// <summary>Resumen para el donante que ya inició sesión (null si es un visitante).</summary>
    public ResumenDonanteHome? Donante { get; set; }
}

public record GatoApadrinableHome(int Id, string Nombre, string? FotoUrl, string Fundacion, string Slug, string? Historia);

public record MunicipioHome(string Nombre, int Fundaciones);

public record ResumenDonanteHome(string Nombre, int DonacionesConfirmadas, decimal TotalDonado, int DonacionesPendientes, int Apadrinamientos);
