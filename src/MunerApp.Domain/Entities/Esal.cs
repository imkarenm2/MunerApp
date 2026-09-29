namespace MunerApp.Domain.Entities;

public class Esal
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Nit { get; set; } = string.Empty;
    public string TipoEntidad { get; set; } = string.Empty;
    public string CorreoContacto { get; set; } = string.Empty;
    public bool Activa { get; set; } = true;
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public ICollection<EsalModulo> Modulos { get; set; } = new List<EsalModulo>();
    public ICollection<RedSocial> RedesSociales { get; set; } = new List<RedSocial>();
    public ConfigPasarela? ConfigPasarela { get; set; }
}
