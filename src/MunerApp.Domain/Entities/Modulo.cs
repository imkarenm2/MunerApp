namespace MunerApp.Domain.Entities;

public class Modulo
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;

    /// <summary>false = módulo general (siempre activo); true = se activa por ESAL.</summary>
    public bool EsConfigurable { get; set; }
}
