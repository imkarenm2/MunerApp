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

/// <summary>Datos del PDF de rendición de cuentas de una causa (HU-046). Las fotos van como bytes (JPG, PNG o WEBP).</summary>
public record DatosRendicionCausa(
    string NombreEsal,
    string NitEsal,
    string TituloCausa,
    decimal Meta,
    decimal Recaudado,
    int Donaciones,
    decimal? ValorEjecutado,
    DateTime FechaLimite,
    string MotivoCierre,
    string Texto,
    IReadOnlyList<byte[]> Fotos,
    IReadOnlyList<string> Soportes,
    DateTime FechaPublicacionUtc);

/// <summary>Un dato de la ficha: etiqueta y valor ya formateado.</summary>
public record CampoFicha(string Etiqueta, string Valor);

/// <summary>Grupo de datos de la ficha, por ejemplo "Reseña" o "Examen de ingreso".</summary>
public record SeccionFicha(string Titulo, IReadOnlyList<CampoFicha> Campos);

/// <summary>Tabla de la ficha, por ejemplo el historial de estados o los eventos clínicos. Los anchos son relativos.</summary>
public record TablaFicha(
    string Titulo,
    IReadOnlyList<string> Columnas,
    IReadOnlyList<float> Anchos,
    IReadOnlyList<IReadOnlyList<string>> Filas,
    string TextoSiVacia);

/// <summary>
/// Datos de un documento descargable de un beneficiario: la hoja de vida o la historia clínica (HU-017 y HU-019).
/// Es información interna: el PDF lo marca como confidencial y dice quién lo descargó.
/// </summary>
public record DatosFichaBeneficiario(
    string TipoDocumento,
    string NombreEsal,
    string NitEsal,
    string NombreBeneficiario,
    string Resumen,
    byte[]? Foto,
    IReadOnlyList<SeccionFicha> Secciones,
    IReadOnlyList<TablaFicha> Tablas,
    string GeneradoPor,
    DateTime FechaGeneracionUtc);

/// <summary>Reportes en PDF de la fundación.</summary>
public interface IReportesService
{
    /// <summary>HU-040 escenario 3: reporte de medicamentos en PDF.</summary>
    byte[] GenerarReporteMedicamentos(DatosReporteMedicamentos datos);

    /// <summary>HU-046 escenario 2: rendición de cuentas de una causa, para la sección de transparencia.</summary>
    byte[] GenerarRendicionCausa(DatosRendicionCausa datos);

    /// <summary>Hoja de vida o historia clínica de un beneficiario en PDF.</summary>
    byte[] GenerarFichaBeneficiario(DatosFichaBeneficiario datos);
}
