using MunerApp.Domain.Common;

namespace MunerApp.Domain.Entities;

/// <summary>Foto de un producto de la tienda. Archivo público (HU-023).</summary>
public class FotoProducto : IPerteneceAEsal
{
    public int Id { get; set; }
    public int EsalId { get; set; }
    public int ProductoId { get; set; }
    public string Ruta { get; set; } = string.Empty;
    public int Orden { get; set; }
    public DateTime FechaCarga { get; set; } = DateTime.UtcNow;

    public Producto? Producto { get; set; }
}
