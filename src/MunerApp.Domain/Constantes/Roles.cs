namespace MunerApp.Domain.Constantes;

public static class Roles
{
    public const string SuperAdministrador = "SuperAdministrador";
    public const string AdministradorESAL = "AdministradorESAL";
    public const string Voluntario = "Voluntario";
    public const string Donante = "Donante";

    /// <summary>Veeduría ciudadana: consulta lo que reciben las fundaciones y en qué se usa, sin datos personales.</summary>
    public const string Veedor = "Veedor";

    public static readonly string[] Todos = { SuperAdministrador, AdministradorESAL, Voluntario, Donante, Veedor };
}
