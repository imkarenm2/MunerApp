using MunerApp.Domain.Common;

namespace MunerApp.Domain.Entities;

/// <summary>
/// Novedad de un beneficiario para sus padrinos (HU-022): texto y fotos sobre cómo avanza.
/// Las fotos son privadas: solo las ven la fundación y los padrinos del beneficiario.
/// </summary>
public class NovedadBeneficiario : IPerteneceAEsal
{
    public const int MaxFotos = 3;

    public int Id { get; set; }
    public int EsalId { get; set; }
    public int BeneficiarioId { get; set; }

    public string Texto { get; set; } = string.Empty;
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public string? PublicadaPorId { get; set; }

    public Beneficiario? Beneficiario { get; set; }
    public ICollection<FotoNovedad> Fotos { get; set; } = new List<FotoNovedad>();
}

/// <summary>Foto de una novedad (HU-022). Archivo privado.</summary>
public class FotoNovedad : IPerteneceAEsal
{
    public int Id { get; set; }
    public int EsalId { get; set; }
    public int NovedadId { get; set; }
    public string Ruta { get; set; } = string.Empty;

    public NovedadBeneficiario? Novedad { get; set; }
}
