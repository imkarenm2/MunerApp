using MunerApp.Domain.Common;
using MunerApp.Domain.Enums;

namespace MunerApp.Domain.Entities;

public class RedSocial : IPerteneceAEsal
{
    public int Id { get; set; }
    public int EsalId { get; set; }
    public TipoRedSocial Tipo { get; set; }
    public string Url { get; set; } = string.Empty;

    public Esal? Esal { get; set; }
}
