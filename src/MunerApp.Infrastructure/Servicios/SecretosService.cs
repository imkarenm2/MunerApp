using Microsoft.AspNetCore.DataProtection;
using MunerApp.Application.Interfaces;

namespace MunerApp.Infrastructure.Servicios;

/// <summary>
/// Cifra secretos con ASP.NET Data Protection. Ojo: lo cifrado en un equipo solo se descifra
/// con las llaves de ese mismo entorno, por eso cada entorno (local / Azure) registra sus propias llaves.
/// </summary>
public class SecretosService : ISecretosService
{
    private readonly IDataProtector _protector;

    public SecretosService(IDataProtectionProvider proveedor)
    {
        _protector = proveedor.CreateProtector("MunerApp.Pasarela.v1");
    }

    public string Proteger(string valor) => _protector.Protect(valor);

    public string Desproteger(string valorProtegido) => _protector.Unprotect(valorProtegido);
}
