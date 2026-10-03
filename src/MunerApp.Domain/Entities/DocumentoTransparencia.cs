using MunerApp.Domain.Common;
using MunerApp.Domain.Enums;

namespace MunerApp.Domain.Entities;

/// <summary>
/// Documento de transparencia (HU-012). Nunca se borra: al ocultarlo deja de verse en el
/// perfil público pero queda en el historial interno de la fundación.
/// </summary>
public class DocumentoTransparencia : IPerteneceAEsal
{
    public int Id { get; set; }
    public int EsalId { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public CategoriaDocumento Categoria { get; set; }
    public string? Descripcion { get; set; }
    public string Ruta { get; set; } = string.Empty;
    public string NombreOriginal { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public long TamanoBytes { get; set; }
    public bool Visible { get; set; } = true;
    public DateTime FechaPublicacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaOcultado { get; set; }
    public string? PublicadoPorId { get; set; }

    public Esal? Esal { get; set; }
}
