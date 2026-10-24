namespace MunerApp.Web.Seguridad;

/// <summary>
/// Límites de uso (seguridad): cuántos envíos acepta un formulario público desde la misma IP.
/// Evitan que un robot pruebe contraseñas, cree cuentas en masa o dispare cientos de correos.
/// </summary>
public static class LimitesUso
{
    /// <summary>Iniciar sesión, registro, recuperar contraseña y formularios que crean datos o envían avisos.</summary>
    public const string Formularios = "formularios";

    /// <summary>
    /// Envíos permitidos por minuto en cada formulario desde la misma IP. Una persona nunca llega a esto; un robot sí.
    /// Es alto porque en una universidad o una misma red wifi muchas personas salen con la misma IP.
    /// </summary>
    public const int EnviosPorMinuto = 30;
}
