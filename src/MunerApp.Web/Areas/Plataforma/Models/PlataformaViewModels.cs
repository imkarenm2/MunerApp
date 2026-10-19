using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using MunerApp.Domain.Enums;
using MunerApp.Web.Areas.Fundacion.Models;

namespace MunerApp.Web.Areas.Plataforma.Models;

// ---------------- Resumen de la plataforma ----------------

public class ResumenPlataformaViewModel
{
    public string NombreUsuario { get; set; } = string.Empty;
    public DateTime Hoy { get; set; }

    public int FundacionesActivas { get; set; }
    public int FundacionesInactivas { get; set; }
    public int Donantes { get; set; }
    public int DonantesNuevosMes { get; set; }

    public decimal DonadoTotal { get; set; }
    public decimal DonadoMes { get; set; }
    public int DonacionesMes { get; set; }

    public int GatosEnCasa { get; set; }
    public int HogaresTotal { get; set; }
    public int HogaresMes { get; set; }

    public int PadrinosActivos { get; set; }
    public decimal AporteMensualPadrinos { get; set; }
    public int VoluntariosActivos { get; set; }
    public int CausasActivas { get; set; }

    public IReadOnlyList<TareaPanel> Tareas { get; set; } = Array.Empty<TareaPanel>();
    public IReadOnlyList<CausaPorAprobarItem> CausasPorAprobar { get; set; } = Array.Empty<CausaPorAprobarItem>();
    public IReadOnlyList<FundacionResumenItem> Fundaciones { get; set; } = Array.Empty<FundacionResumenItem>();
}

public record CausaPorAprobarItem(int Id, string Titulo, string Fundacion, decimal Meta, DateTime Fecha, bool Corregida);

public class FundacionResumenItem
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? Ciudad { get; set; }
    public bool Activa { get; set; }
    public bool EnDirectorio { get; set; }
    public int Gatos { get; set; }
    public decimal DonadoMes { get; set; }
    public int DonacionesPendientes { get; set; }
    public int CausasActivas { get; set; }
    public int Padrinos { get; set; }
}

// ---------------- Causas ----------------

public enum FiltroCausas
{
    PorAprobar,
    Publicadas,
    Rechazadas,
    Todas
}

public class CausasPlataformaViewModel
{
    public FiltroCausas Filtro { get; set; }
    public IReadOnlyDictionary<FiltroCausas, int> Conteos { get; set; } = new Dictionary<FiltroCausas, int>();
    public IReadOnlyList<CausaPlataformaItem> Causas { get; set; } = Array.Empty<CausaPlataformaItem>();
}

public class CausaPlataformaItem
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Fundacion { get; set; } = string.Empty;
    public string? SlugFundacion { get; set; }
    public decimal Meta { get; set; }
    public decimal Recaudado { get; set; }
    public DateTime FechaLimite { get; set; }
    public DateTime FechaCreacion { get; set; }
    public EstadoCausa Estado { get; set; }
    public bool Cerrada { get; set; }
    public bool CreadaPorPlataforma { get; set; }
}

public class RevisarCausaViewModel
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string? Justificacion { get; set; }
    public decimal Meta { get; set; }
    public decimal Recaudado { get; set; }
    public DateTime FechaLimite { get; set; }
    public DateTime FechaCreacion { get; set; }
    public EstadoCausa Estado { get; set; }
    public bool Cerrada { get; set; }
    public string? MotivoRechazo { get; set; }
    public string? RevisadaPor { get; set; }
    public DateTime? FechaRevision { get; set; }
    public string? CreadaPor { get; set; }
    public IReadOnlyList<string> Fotos { get; set; } = Array.Empty<string>();

    public int EsalId { get; set; }
    public string Fundacion { get; set; } = string.Empty;
    public string? SlugFundacion { get; set; }
    public string? CiudadFundacion { get; set; }
    public bool FundacionActiva { get; set; }
    public bool FundacionTieneDatosDonacion { get; set; }
    public int CausasAnteriores { get; set; }

    [StringLength(500, MinimumLength = 10, ErrorMessage = "Explica el motivo en {2} a {1} caracteres.")]
    [Display(Name = "¿Qué debe corregir la fundación?")]
    public string? Motivo { get; set; }

    public bool EnRevision => Estado is EstadoCausa.PorAprobar or EstadoCausa.Rechazada;
}

/// <summary>El superadministrador crea la causa a nombre de una fundación: se publica de una vez.</summary>
public class CausaPlataformaFormViewModel : CausaFormViewModel
{
    [Required(ErrorMessage = "Selecciona la fundación.")]
    [Display(Name = "Fundación")]
    public int? EsalId { get; set; }

    public IReadOnlyList<SelectListItem> Fundaciones { get; set; } = Array.Empty<SelectListItem>();
}

// ---------------- Informes ----------------

public record BarraMes(string Mes, decimal Valor, int Cantidad);
public record FilaTotal(string Nombre, decimal Valor, int Cantidad);

public class InformeDonacionesViewModel
{
    public int? EsalId { get; set; }
    public IReadOnlyList<SelectListItem> Fundaciones { get; set; } = Array.Empty<SelectListItem>();

    public decimal Confirmado { get; set; }
    public int CantidadConfirmadas { get; set; }
    public decimal Pendiente { get; set; }
    public int CantidadPendientes { get; set; }
    public int CantidadRechazadas { get; set; }
    public int DonantesDistintos { get; set; }
    public decimal Promedio => CantidadConfirmadas == 0 ? 0 : Math.Round(Confirmado / CantidadConfirmadas);

    /// <summary>Días que tarda, en promedio, una fundación en revisar una donación.</summary>
    public double? DiasPromedioRevision { get; set; }

    public IReadOnlyList<BarraMes> PorMes { get; set; } = Array.Empty<BarraMes>();
    public IReadOnlyList<FilaTotal> PorDestino { get; set; } = Array.Empty<FilaTotal>();
    public IReadOnlyList<FilaTotal> PorFundacion { get; set; } = Array.Empty<FilaTotal>();
    public IReadOnlyList<DonacionInformeItem> Recientes { get; set; } = Array.Empty<DonacionInformeItem>();
}

/// <summary>Sin datos del donante: el informe es para seguimiento, no para ver quién dona.</summary>
public record DonacionInformeItem(string Codigo, string Fundacion, string Destino, decimal Valor, EstadoDonacion Estado, DateTime FechaReporte);

public class InformeApadrinamientosViewModel
{
    public int? EsalId { get; set; }
    public IReadOnlyList<SelectListItem> Fundaciones { get; set; } = Array.Empty<SelectListItem>();

    public int Activos { get; set; }
    public int Cancelados { get; set; }
    public int PadrinosDistintos { get; set; }
    public int GatosApadrinados { get; set; }
    public int GatosApadrinables { get; set; }
    public decimal AporteMensual { get; set; }
    public decimal AporteRecibidoMes { get; set; }

    public IReadOnlyList<BarraMes> NuevosPorMes { get; set; } = Array.Empty<BarraMes>();
    public IReadOnlyList<FilaApadrinamientoFundacion> PorFundacion { get; set; } = Array.Empty<FilaApadrinamientoFundacion>();
    public IReadOnlyList<ApadrinamientoInformeItem> Recientes { get; set; } = Array.Empty<ApadrinamientoInformeItem>();
}

public record FilaApadrinamientoFundacion(string Fundacion, int Activos, int Apadrinables, decimal AporteMensual);
public record ApadrinamientoInformeItem(string Fundacion, string Gato, decimal ValorMensual, EstadoApadrinamiento Estado, DateTime FechaInicio);
