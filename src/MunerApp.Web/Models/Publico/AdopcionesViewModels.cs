namespace MunerApp.Web.Models.Publico;

/// <summary>HU-029: recomendaciones y responsabilidades antes del formulario de adopción.</summary>
public class RecomendacionesAdopcionViewModel
{
    // Datos para mostrar
    public string Slug { get; set; } = string.Empty;
    public string NombreEsal { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public IReadOnlyList<string> Recomendaciones { get; set; } = Array.Empty<string>();

    /// <summary>Si ya tiene una solicitud en trámite o es una cuenta de fundación, se muestra el aviso en vez de la aceptación.</summary>
    public string? Aviso { get; set; }

    /// <summary>Si ya aceptó antes y tiene un borrador, el botón dice "Continuar mi solicitud".</summary>
    public bool TieneBorrador { get; set; }

    /// <summary>Escenario 1: debe marcar que acepta para habilitar el formulario.</summary>
    public bool Acepto { get; set; }
}

/// <summary>Formulario de adopción por secciones (HU-030 a HU-032).</summary>
public class FormularioAdopcionViewModel
{
    public string Slug { get; set; } = string.Empty;
    public string NombreEsal { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
}
