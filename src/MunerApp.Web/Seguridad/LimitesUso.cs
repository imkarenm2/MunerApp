namespace MunerApp.Web.Seguridad;

/// <summary>
/// Límites de uso (seguridad): cuántos envíos acepta un formulario público desde la misma IP.
/// Evitan que un robot pruebe contraseñas, cree cuentas en masa o dispare cientos de correos.
/// </summary>
public static class LimitesUso
{
    /// <summary>Iniciar sesión, registro, recuperar contraseña y formularios que crean datos o envían avisos.</summary>
    public const string Formularios = "formularios";

    /// <summary>Envíos permitidos por minuto: una persona nunca llega a esto; un robot sí.</summary>
    public const int EnviosPorMinuto = 10;
}
