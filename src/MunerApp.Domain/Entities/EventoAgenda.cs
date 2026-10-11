using MunerApp.Domain.Common;
using MunerApp.Domain.Enums;

namespace MunerApp.Domain.Entities;

/// <summary>
/// Evento programado en la agenda de salud de un beneficiario: medicación, vacuna o control (HU-038).
/// Un tratamiento recurrente genera un evento por dosis, todos con el mismo <see cref="SerieId"/>.
/// Al marcarlo como realizado se registra en la historia clínica (<see cref="EventoClinico"/>, HU-019).
/// </summary>
public class EventoAgenda : IPerteneceAEsal
{
    /// <summary>Tope de dosis que puede generar un tratamiento, para evitar errores de digitación.</summary>
    public const int MaxDosisPorTratamiento = 200;

    public int Id { get; set; }
    public int EsalId { get; set; }
    public int BeneficiarioId { get; set; }

    /// <summary>Se usan los mismos tipos de la historia clínica; "Tratamiento" es la medicación.</summary>
    public TipoEventoClinico Tipo { get; set; }

    /// <summary>Fecha y hora programadas (UTC).</summary>
    public DateTime FechaProgramada { get; set; }

    public string Descripcion { get; set; } = string.Empty;

    /// <summary>Medicamento del inventario (HU-037), opcional.</summary>
    public int? MedicamentoId { get; set; }
    public string? Dosis { get; set; }

    /// <summary>Tratamiento recurrente: todas sus dosis comparten la serie. Null si es un evento único.</summary>
    public Guid? SerieId { get; set; }
    public int NumeroDosis { get; set; } = 1;
    public int TotalDosis { get; set; } = 1;

    public EstadoEventoAgenda Estado { get; set; } = EstadoEventoAgenda.Pendiente;

    public string? CreadoPorId { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    // Escenario 2: realizado y registrado en la historia clínica
    public DateTime? FechaRealizado { get; set; }
    public string? RealizadoPorId { get; set; }
    public int? EventoClinicoId { get; set; }

    /// <summary>HU-039 escenario 3: ya se envió el recordatorio del día anterior.</summary>
    public bool RecordatorioEnviado { get; set; }

    public DateTime? FechaCancelado { get; set; }
    public string? CanceladoPorId { get; set; }
    public string? MotivoCancelacion { get; set; }

    public Beneficiario? Beneficiario { get; set; }
    public Medicamento? Medicamento { get; set; }
    public EventoClinico? EventoClinico { get; set; }

    public bool EsRecurrente => SerieId is not null;

    public bool EstaAtrasado(DateTime ahoraUtc) => Estado == EstadoEventoAgenda.Pendiente && FechaProgramada < ahoraUtc;

    /// <summary>Duración de un intervalo de frecuencia, o null si está fuera de los límites permitidos.</summary>
    public static TimeSpan? Intervalo(int cada, UnidadFrecuencia unidad) => unidad switch
    {
        UnidadFrecuencia.Horas when cada is >= 1 and <= 72 => TimeSpan.FromHours(cada),
        UnidadFrecuencia.Dias when cada is >= 1 and <= 90 => TimeSpan.FromDays(cada),
        _ => null
    };

    /// <summary>
    /// Escenario 3: fechas de todas las dosis de un tratamiento que empieza en <paramref name="inicio"/>,
    /// se repite cada <paramref name="intervalo"/> y dura <paramref name="duracionDias"/> días.
    /// Ej.: cada 8 horas durante 5 días son 15 dosis; la última es antes de que se cumplan los 5 días.
    /// </summary>
    public static List<DateTime> FechasDeTratamiento(DateTime inicio, TimeSpan intervalo, int duracionDias)
    {
        if (intervalo <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(intervalo));
        if (duracionDias < 1) throw new ArgumentOutOfRangeException(nameof(duracionDias));

        var fin = inicio.AddDays(duracionDias);
        var fechas = new List<DateTime>();
        for (var f = inicio; f < fin && fechas.Count <= MaxDosisPorTratamiento; f = f.Add(intervalo))
            fechas.Add(f);
        return fechas; // si supera el tope trae MaxDosisPorTratamiento + 1, para que quien llama lo rechace
    }
}
