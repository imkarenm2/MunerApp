namespace MunerApp.Web.Models.Publico;

/// <summary>Una buena noticia que se arma sola con lo que pasa en la plataforma (HU-028): una adopción, una causa cumplida...</summary>
public record BuenaNoticia(string Icono, string Clase, string Titulo, string Detalle, string? Url, DateTime Fecha);

/// <summary>
/// "El periódico de MunerApp" (HU-028): portada pública con lo bueno que pasa en las fundaciones.
/// Lo ve cualquier visitante, sin cuenta, en el inicio y en /boletin.
/// </summary>
public class PeriodicoViewModel
{
    /// <summary>La nota principal: el próximo evento o la publicación más reciente.</summary>
    public PublicacionTarjeta? Titular { get; set; }
    public IReadOnlyList<PublicacionTarjeta> ProximosEventos { get; set; } = Array.Empty<PublicacionTarjeta>();
    public IReadOnlyList<PublicacionTarjeta> Recientes { get; set; } = Array.Empty<PublicacionTarjeta>();
    public IReadOnlyList<BuenaNoticia> BuenasNoticias { get; set; } = Array.Empty<BuenaNoticia>();

    // Cifras de toda la plataforma (solo totales; nunca datos de donantes)
    public int Fundaciones { get; set; }
    public int Adopciones { get; set; }
    public int CausasCumplidas { get; set; }
    public decimal TotalDonado { get; set; }

    public bool TieneContenido => Titular is not null || BuenasNoticias.Count > 0;
}
