namespace MunerApp.Domain.Entities;

public class Esal
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Nit { get; set; } = string.Empty;
    public string TipoEntidad { get; set; } = string.Empty;
    public string CorreoContacto { get; set; } = string.Empty;
    public bool Activa { get; set; } = true;
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    // ---- Perfil institucional (HU-009) ----

    /// <summary>Parte de la dirección pública: /fundaciones/{Slug}. Se genera a partir del nombre.</summary>
    public string? Slug { get; set; }

    /// <summary>Frase corta para las tarjetas del directorio (HU-010).</summary>
    public string? DescripcionCorta { get; set; }
    public string? Historia { get; set; }
    public string? Mision { get; set; }
    public string? Vision { get; set; }
    public string? Ciudad { get; set; }
    public string? Telefono { get; set; }

    /// <summary>Clave del archivo del logo en el almacenamiento público.</summary>
    public string? LogoRuta { get; set; }

    /// <summary>Si es true, la fundación no está recibiendo voluntarios por ahora (HU-016, HU-035).</summary>
    public bool VoluntariadoPausado { get; set; }

    public DateTime? FechaActualizacionPerfil { get; set; }

    public ICollection<EsalModulo> Modulos { get; set; } = new List<EsalModulo>();
    public ICollection<RedSocial> RedesSociales { get; set; } = new List<RedSocial>();
    public ICollection<FotoEsal> Fotos { get; set; } = new List<FotoEsal>();
    public ICollection<DocumentoTransparencia> Documentos { get; set; } = new List<DocumentoTransparencia>();
    public ConfigPasarela? ConfigPasarela { get; set; }
    public DatosDonacion? DatosDonacion { get; set; }
}
