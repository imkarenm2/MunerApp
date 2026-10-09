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
        EstadoPostulacion.Retirada => "Desvinculado",
        _ => e.ToString()
    };

    public static string De(SexoBeneficiario s) => s switch
    {
        SexoBeneficiario.Macho => "Macho",
        SexoBeneficiario.Hembra => "Hembra",
        _ => s.ToString()
    };

    public static string De(EstadoBeneficiario e) => e switch
    {
        EstadoBeneficiario.EnLaFundacion => "En la fundación",
        EstadoBeneficiario.EnTratamiento => "En tratamiento",
        EstadoBeneficiario.Adoptable => "Adoptable",
        EstadoBeneficiario.Adoptado => "Adoptado",
        EstadoBeneficiario.Fallecido => "Fallecido",
        _ => e.ToString()
    };

    public static string De(TipoEventoClinico t) => t switch
    {
        TipoEventoClinico.Vacuna => "Vacuna",
        TipoEventoClinico.Tratamiento => "Tratamiento",
        TipoEventoClinico.Control => "Control",
        TipoEventoClinico.Otro => "Otro",
        _ => t.ToString()
    };

    public static string De(EstadoApadrinamiento e) => e switch
    {
        EstadoApadrinamiento.Activo => "Activo",
        EstadoApadrinamiento.Cancelado => "Cancelado",
        _ => e.ToString()
    };

    public static string De(EstadoCausa e) => e switch
    {
        EstadoCausa.Activa => "Activa",
        EstadoCausa.Pausada => "En pausa",
        EstadoCausa.Cerrada => "Cerrada",
        _ => e.ToString()
    };

    public static string De(EstadoSolicitudAdopcion e) => e switch
    {
        EstadoSolicitudAdopcion.Borrador => "En diligenciamiento",
        EstadoSolicitudAdopcion.Recibida => "Recibida",
        EstadoSolicitudAdopcion.AprobadaParaCita => "Aprobada para cita",
        EstadoSolicitudAdopcion.Rechazada => "Rechazada",
        EstadoSolicitudAdopcion.CitaAgendada => "Cita agendada",
        EstadoSolicitudAdopcion.AdopcionConcretada => "Adopción concretada",
        EstadoSolicitudAdopcion.NoConcretada => "Adopción no concretada",
        _ => e.ToString()
    };

    public static string De(OcupacionAdoptante o) => o switch
    {
        OcupacionAdoptante.Empleado => "Empleado",
        OcupacionAdoptante.Independiente => "Independiente",
        OcupacionAdoptante.Estudiante => "Estudiante",
        OcupacionAdoptante.Pensionado => "Pensionado",
        OcupacionAdoptante.Hogar => "Labores del hogar",
        OcupacionAdoptante.Desempleado => "Sin empleo actualmente",
        _ => o.ToString()
    };

    public static string De(TenenciaMascotas t) => t switch
    {
        TenenciaMascotas.Tengo => "Sí, tengo mascotas",
        TenenciaMascotas.Tuve => "Sí tuve, pero ya no",
        TenenciaMascotas.Nunca => "No, nunca he tenido",
        _ => t.ToString()
    };

    public static string De(EsterilizacionMascotas e) => e switch
    {
        EsterilizacionMascotas.Todos => "Sí, todos",
        EsterilizacionMascotas.Algunos => "Algunos",
        EsterilizacionMascotas.Ninguno => "Ninguno",
        _ => e.ToString()
    };

    public static string De(VacunasGato v) => v switch
    {
        VacunasGato.Completas => "a) Sí, tienen todas sus vacunas",
        VacunasGato.Ninguna => "b) No están vacunados",
        VacunasGato.Parciales => "c) Tienen algunas vacunas",
        _ => v.ToString()
    };

    public static string De(SociabilidadPerro s) => s switch
    {
        SociabilidadPerro.Sociable => "Sí, es sociable con gatos",
        SociabilidadPerro.NoSociable => "No es sociable con gatos",
        SociabilidadPerro.NoSabe => "No sé, no ha convivido con gatos",
        _ => s.ToString()
    };

    public static string De(TipoVivienda t) => t switch
    {
        TipoVivienda.Casa => "Casa",
        TipoVivienda.Apartamento => "Apartamento",
        TipoVivienda.Finca => "Finca",
        TipoVivienda.Habitacion => "Habitación",
        _ => t.ToString()
    };

    public static string De(TenenciaVivienda t) => t switch
    {
        TenenciaVivienda.Propia => "Propia",
        TenenciaVivienda.Arrendada => "Arrendada",
        TenenciaVivienda.Familiar => "Familiar",
        _ => t.ToString()
    };

    public static string De(ViaAdministracion v) => v switch
    {
        ViaAdministracion.Oral => "Oral",
        ViaAdministracion.Topica => "Tópica",
        ViaAdministracion.Oftalmica => "Oftálmica",
        ViaAdministracion.Otica => "Ótica",
        ViaAdministracion.Subcutanea => "Subcutánea",
        ViaAdministracion.Intramuscular => "Intramuscular",
        ViaAdministracion.Intravenosa => "Intravenosa",
        _ => "Otra"
    };

    public static string De(PresentacionMedicamento p) => p switch
    {
        PresentacionMedicamento.Tabletas => "Tabletas",
        PresentacionMedicamento.Capsulas => "Cápsulas",
        PresentacionMedicamento.Mililitros => "Mililitros (ml)",
        PresentacionMedicamento.Frascos => "Frascos",
        PresentacionMedicamento.Ampollas => "Ampollas",
        PresentacionMedicamento.Sobres => "Sobres",
        PresentacionMedicamento.Tubos => "Tubos",
        _ => "Unidades"
    };

    /// <summary>Unidad corta para mostrar junto a una cantidad: "12 tabletas", "40 ml".</summary>
    public static string UnidadDe(PresentacionMedicamento p) => p switch
    {
        PresentacionMedicamento.Mililitros => "ml",
        _ => De(p).ToLowerInvariant()
    };

    public static string De(TipoMovimientoMedicamento t) => t switch
    {
        TipoMovimientoMedicamento.Registro => "Registro inicial",
        TipoMovimientoMedicamento.Entrada => "Entrada",
        TipoMovimientoMedicamento.Uso => "Uso",
        _ => t.ToString()
    };

    public static string De(EstadoEventoAgenda e) => e switch
    {
        EstadoEventoAgenda.Pendiente => "Pendiente",
        EstadoEventoAgenda.Realizado => "Realizado",
        EstadoEventoAgenda.Cancelado => "Cancelado",
        _ => e.ToString()
    };

    public static string De(UnidadFrecuencia u, int cantidad) => u switch
    {
        UnidadFrecuencia.Horas => cantidad == 1 ? "hora" : "horas",
        UnidadFrecuencia.Dias => cantidad == 1 ? "día" : "días",
        _ => u.ToString()
    };
}
