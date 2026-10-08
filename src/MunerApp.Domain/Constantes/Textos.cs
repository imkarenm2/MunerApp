using MunerApp.Domain.Enums;

namespace MunerApp.Domain.Constantes;

/// <summary>Textos en español de los enums que se muestran en pantalla.</summary>
public static class Textos
{
    public static string De(CategoriaDocumento c) => c switch
    {
        CategoriaDocumento.RegistroLegal => "Registro legal",
        CategoriaDocumento.Certificado => "Certificado",
        CategoriaDocumento.RendicionCuentas => "Rendición de cuentas",
        CategoriaDocumento.EstadosFinancieros => "Estados financieros",
        _ => "Otro documento"
    };

    public static string De(TipoCuentaDonacion t) => t switch
    {
        TipoCuentaDonacion.Llave => "Llave Bre-B",
        TipoCuentaDonacion.Ahorros => "Cuenta de ahorros",
        TipoCuentaDonacion.Corriente => "Cuenta corriente",
        TipoCuentaDonacion.BilleteraDigital => "Billetera digital (Nequi, Daviplata…)",
        _ => t.ToString()
    };

    public static string De(TipoLlave t) => t switch
    {
        TipoLlave.Documento => "NIT",
        TipoLlave.Celular => "Celular",
        TipoLlave.Correo => "Correo electrónico",
        TipoLlave.Alfanumerica => "Alfanumérica (@)",
        _ => t.ToString()
    };

    /// <summary>"Llave Bre-B (celular)", "Cuenta de ahorros"...</summary>
    public static string De(TipoCuentaDonacion t, TipoLlave? llave) =>
        t != TipoCuentaDonacion.Llave || llave is null ? De(t) : llave switch
        {
            TipoLlave.Documento => "Llave Bre-B (NIT)",
            TipoLlave.Celular => "Llave Bre-B (celular)",
            TipoLlave.Correo => "Llave Bre-B (correo)",
            _ => "Llave Bre-B (alfanumérica)"
        };

    public static string De(EstadoDonacion e) => e switch
    {
        EstadoDonacion.Pendiente => "Pendiente de confirmación",
        EstadoDonacion.Confirmada => "Confirmada",
        EstadoDonacion.Rechazada => "Rechazada",
        _ => e.ToString()
    };

    public static string De(TipoPostulacion t) => t switch
    {
        TipoPostulacion.General => "Voluntario general",
        TipoPostulacion.PracticanteSalud => "Practicante de salud",
        _ => t.ToString()
    };

    public static string De(EstadoPostulacion e) => e switch
    {
        EstadoPostulacion.Pendiente => "Pendiente",
        EstadoPostulacion.Aprobada => "Aprobada",
        EstadoPostulacion.Rechazada => "Rechazada",
        _ => e.ToString()
    };
}
