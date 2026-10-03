namespace MunerApp.Domain.Enums;

/// <summary>
/// Tipos de llave Bre-B (Banco de la República). La de código de comercio no se incluye:
/// aplica solo para establecimientos de comercio.
/// </summary>
public enum TipoLlave
{
    /// <summary>NIT de la fundación (solo números, sin dígito de verificación).</summary>
    Documento,

    /// <summary>Celular colombiano de 10 dígitos.</summary>
    Celular,

    Correo,

    /// <summary>Empieza con @ seguida de letras y números, por ejemplo @reinogatos.</summary>
    Alfanumerica
}
