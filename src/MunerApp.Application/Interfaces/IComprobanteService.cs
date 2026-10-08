namespace MunerApp.Application.Interfaces;

/// <summary>Datos que aparecen en el comprobante de una donación confirmada (HU-015).</summary>
public record DatosComprobante(
    string Codigo,
    string NombreDonante,
    string CorreoDonante,
    string NombreEsal,
    string NitEsal,
    decimal Valor,
    DateTime FechaTransferencia,
    DateTime FechaConfirmacion,
    string MedioPago,
    string? ReferenciaPago);

/// <summary>Generación de documentos PDF (comprobantes; en sprints siguientes, certificados y reportes).</summary>
public interface IComprobanteService
{
    byte[] GenerarComprobanteDonacion(DatosComprobante datos);
}
