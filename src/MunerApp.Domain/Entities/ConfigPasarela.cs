using MunerApp.Domain.Common;
using MunerApp.Domain.Enums;

namespace MunerApp.Domain.Entities;

/// <summary>
/// Llaves de Wompi de la ESAL (HU-045). El dinero llega a la cuenta Wompi de la fundación;
/// MunerApp nunca recibe ni reparte dinero. Los secretos se guardan cifrados.
/// </summary>
public class ConfigPasarela : IPerteneceAEsal
{
    public int EsalId { get; set; }
    public string Proveedor { get; set; } = "WOMPI";
    public string LlavePublica { get; set; } = string.Empty;
    public string SecretoIntegridadCifrado { get; set; } = string.Empty;
    public string SecretoEventosCifrado { get; set; } = string.Empty;
    public AmbientePasarela Ambiente { get; set; } = AmbientePasarela.Sandbox;
    public bool Activa { get; set; }

    public Esal? Esal { get; set; }
}
