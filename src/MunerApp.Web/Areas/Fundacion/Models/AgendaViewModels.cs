using System.ComponentModel.DataAnnotations;
using MunerApp.Domain.Enums;

namespace MunerApp.Web.Areas.Fundacion.Models;

/// <summary>HU-038 escenarios 1 y 3: programar un evento único o un tratamiento recurrente.</summary>
public class ProgramarEventoViewModel
{
    [Required(ErrorMessage = "Selecciona el beneficiario.")]
    [Display(Name = "Beneficiario")]
    public int? BeneficiarioId { get; set; }

    [Required(ErrorMessage = "Selecciona el tipo de evento.")]
    [Display(Name = "Tipo de evento")]
    public TipoEventoClinico? Tipo { get; set; }

    [Required(ErrorMessage = "Describe el evento.")]
    [StringLength(300, MinimumLength = 3, ErrorMessage = "La descripción debe tener entre {2} y {1} caracteres.")]
    [Display(Name = "Descripción")]
    public string Descripcion { get; set; } = string.Empty;

    [Display(Name = "Medicamento del inventario (opcional)")]
    public int? MedicamentoId { get; set; }

    [StringLength(150, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Dosis (opcional)")]
    public string? Dosis { get; set; }

    [Required(ErrorMessage = "Ingresa la fecha.")]
    [Display(Name = "Fecha")]
    public DateTime? Fecha { get; set; }

    [Required(ErrorMessage = "Ingresa la hora.")]
    [Display(Name = "Hora")]
    public TimeSpan? Hora { get; set; }

    /// <summary>Escenario 3: tratamiento de varias dosis.</summary>
    public bool Recurrente { get; set; }

    /// <summary>Texto y no número, para que los mensajes de validación salgan siempre en español.</summary>
    [Display(Name = "Cada")]
    public string? FrecuenciaCada { get; set; }

    public UnidadFrecuencia FrecuenciaUnidad { get; set; } = UnidadFrecuencia.Horas;

    [Display(Name = "Durante (días)")]
    public string? DuracionDias { get; set; }

    // Para mostrar el formulario
    public string? NombreBeneficiario { get; set; }
    public List<BeneficiarioOpcion> Beneficiarios { get; set; } = new();
    public List<MedicamentoOpcion> Medicamentos { get; set; } = new();
    public bool HayInventario => Medicamentos.Count > 0;
}

public record MedicamentoOpcion(int Id, string Nombre, string Dosis, PresentacionMedicamento Presentacion);

public class EventoAgendaItem
{
    public int Id { get; set; }
    public int BeneficiarioId { get; set; }
    public string Beneficiario { get; set; } = string.Empty;
    public TipoEventoClinico Tipo { get; set; }
    public DateTime FechaProgramada { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public string? Medicamento { get; set; }
    public string? Dosis { get; set; }
    public Guid? SerieId { get; set; }
    public bool EsRecurrente => SerieId is not null;

    /// <summary>Primera dosis pendiente de su tratamiento en la lista: allí se ofrece suspenderlo.</summary>
    public bool EsProximaDosis { get; set; }
    public int NumeroDosis { get; set; }
    public int TotalDosis { get; set; }
    public EstadoEventoAgenda Estado { get; set; }
    public DateTime? FechaRealizado { get; set; }
    public string? RealizadoPor { get; set; }
    public string? MotivoCancelacion { get; set; }
    public bool Atrasado { get; set; }
}

/// <summary>Eventos de un mismo día (hora de Colombia), para mostrar la agenda agrupada.</summary>
public record DiaAgenda(DateTime Dia, List<EventoAgendaItem> Eventos);

public enum VistaAgenda
{
    Proximos,
    Atrasados,
    MasAdelante,
    Realizados
}

/// <summary>HU-038 escenario 1: agenda general de la fundación.</summary>
public class AgendaGeneralViewModel
{
    public const int DiasProximos = 7;

    public VistaAgenda Vista { get; set; }
    public List<DiaAgenda> Dias { get; set; } = new();
    public int Atrasados { get; set; }
    public int Proximos { get; set; }
    public int MasAdelante { get; set; }

    public static readonly (VistaAgenda Vista, string Texto, string Icono)[] Pestanas =
    {
        (VistaAgenda.Proximos, $"Próximos {DiasProximos} días", "bi-calendar-week"),
        (VistaAgenda.Atrasados, "Atrasados", "bi-exclamation-triangle"),
        (VistaAgenda.MasAdelante, "Más adelante", "bi-calendar3"),
        (VistaAgenda.Realizados, "Realizados", "bi-check2-circle")
    };
}

/// <summary>HU-038 escenario 1: agenda de un beneficiario.</summary>
public class AgendaBeneficiarioViewModel
{
    public int BeneficiarioId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public EstadoBeneficiario Estado { get; set; }
    public DateTime FechaNacimiento { get; set; }
    public bool PuedeProgramar { get; set; }
    public List<DiaAgenda> Pendientes { get; set; } = new();
    public List<EventoAgendaItem> Historial { get; set; } = new();
}

/// <summary>HU-038 escenario 2: marcar un evento como realizado.</summary>
public class RealizarEventoViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Ingresa la fecha en que se realizó.")]
    [Display(Name = "Fecha en que se realizó")]
    public DateTime? Fecha { get; set; }

    [Required(ErrorMessage = "Ingresa quién atendió o aplicó.")]
    [StringLength(150, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Responsable")]
    public string Responsable { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Observaciones (opcional)")]
    public string? Nota { get; set; }

    /// <summary>Cantidad a descontar del inventario; vacía si no se descuenta.</summary>
    [Display(Name = "Descontar del inventario (opcional)")]
    public string? CantidadDescontar { get; set; }

    /// <summary>A dónde volver: "beneficiario" o la agenda general.</summary>
    public string? Volver { get; set; }

    // Para mostrar el formulario
    public EventoAgendaItem Evento { get; set; } = new();
    public decimal? Disponible { get; set; }
    public PresentacionMedicamento? Presentacion { get; set; }
}
