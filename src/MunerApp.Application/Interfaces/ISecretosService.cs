namespace MunerApp.Application.Interfaces;

/// <summary>Cifra y descifra secretos (por ejemplo, las llaves de Wompi) antes de guardarlos.</summary>
public interface ISecretosService
{
    string Proteger(string valor);
    string Desproteger(string valorProtegido);
}
