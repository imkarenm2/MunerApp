namespace MunerApp.Domain.Constantes;

public static class Roles
{
    public const string SuperAdministrador = "SuperAdministrador";
    public const string AdministradorESAL = "AdministradorESAL";
    public const string Voluntario = "Voluntario";
    public const string Donante = "Donante";

    public static readonly string[] Todos = { SuperAdministrador, AdministradorESAL, Voluntario, Donante };
}
