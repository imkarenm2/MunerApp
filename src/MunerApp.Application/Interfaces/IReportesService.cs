namespace MunerApp.Application.Interfaces;

/// <summary>Una fila del reporte de medicamentos (HU-040).</summary>
public record FilaReporteMedicamento(
    string NombreComercial,
    string PrincipioActivo,
    DateTime FechaVencimiento,
    string Uso,
    string Via,
    string Dosis,
    string Cantidad,
    string Estado);

/// <summary>Datos del reporte de medicamentos (HU-040). Los textos ya vienen formateados.</summary>
public record DatosReporteMedicamentos(
    string NombreEsal,
    string NitEsal,
    string Filtro,
    DateTime FechaGeneracionUtc,
    IReadOnlyList<FilaReporteMedicamento> Filas,
    int Vencidos,
    int PorVencer,
    int StockBajo);

/// <summary>Reportes en PDF de la fundación.</summary>
public interface IReportesService
{
    /// <summary>HU-040 escenario 3: reporte de medicamentos en PDF.</summary>
    byte[] GenerarReporteMedicamentos(DatosReporteMedicamentos datos);
}
