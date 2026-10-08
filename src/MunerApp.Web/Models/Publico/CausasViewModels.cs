using MunerApp.Domain.Entities;

namespace MunerApp.Web.Models.Publico;

/// <summary>Tarjeta de una causa de recaudación para el público (HU-042).</summary>
public class CausaTarjeta
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string? FotoUrl { get; set; }
    public decimal Meta { get; set; }
    public decimal Recaudado { get; set; }
    public int Porcentaje { get; set; }
    public int DiasRestantes { get; set; }
    public DateTime FechaLimite { get; set; }

    /// <summary>Llegó a la meta o a su fecha límite: no recibe donaciones.</summary>
    public bool Cerrada { get; set; }

    /// <summary>La fundación la pausó: se ve, pero no recibe donaciones por ahora.</summary>
    public bool Pausada { get; set; }

    public string NombreEsal { get; set; } = string.Empty;
    public string SlugEsal { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }

    /// <summary>En el listado de todas las fundaciones se muestra a cuál pertenece cada causa.</summary>
    public bool MostrarFundacion { get; set; }

    public bool RecibeDonaciones => !Cerrada && !Pausada;
    public string Url => $"/fundaciones/{SlugEsal}/causas/{Id}";
}

public class CausasPublicasViewModel
{
    public bool HayCausas => Abiertas.Count + Cerradas.Count > 0;
    public IReadOnlyList<CausaTarjeta> Abiertas { get; set; } = Array.Empty<CausaTarjeta>();
    public IReadOnlyList<CausaTarjeta> Cerradas { get; set; } = Array.Empty<CausaTarjeta>();
}

public class CausaDetalleViewModel : CausaTarjeta
{
    public string Descripcion { get; set; } = string.Empty;
    public IReadOnlyList<string> Fotos { get; set; } = Array.Empty<string>();
    public DateTime FechaPublicacion { get; set; }

    /// <summary>Enlace a la rendición de cuentas de la causa, si ya existe (HU-042, escenario 3).</summary>
    public string? RendicionUrl { get; set; }

    public bool PagosEnLineaActivos { get; set; }
}
