using MunerApp.Application.Interfaces;
using MunerApp.Domain.Entities;
using MunerApp.Infrastructure.Identity;

namespace MunerApp.Web.Servicios;

public static class Comprobantes
{
    /// <summary>Arma los datos del comprobante de una donación confirmada (requiere Esal cargada).</summary>
    public static DatosComprobante Datos(Donacion d, Usuario donante) => new(
        d.Codigo, donante.NombreCompleto, donante.Email ?? string.Empty, d.Esal!.Nombre, d.Esal.Nit,
        d.Valor, d.FechaTransferencia, d.FechaRevision ?? DateTime.UtcNow, d.MedioPago, d.ReferenciaPago);
}
