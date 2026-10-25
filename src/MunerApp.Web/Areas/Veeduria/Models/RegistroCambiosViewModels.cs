using MunerApp.Domain.Enums;

namespace MunerApp.Web.Areas.Veeduria.Models;

/// <summary>Trazabilidad: consulta del registro de auditoría (quién cambió qué y cuándo).</summary>
public class RegistroCambiosViewModel
{
    // Filtros
    public int? EsalId { get; set; }
    public string? Entidad { get; set; }
    public AccionAuditoria? Accion { get; set; }
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }

    public List<(int Id, string Nombre)> Fundaciones { get; set; } = new();
    public List<string> Entidades { get; set; } = new();

    public List<RegistroCambioItem> Registros { get; set; } = new();
    public int Pagina { get; set; } = 1;
    public int TotalPaginas { get; set; } = 1;
    public int Total { get; set; }

    /// <summary>El veedor no ve datos personales: los nombres de donantes y algunos campos se ocultan.</summary>
    public bool OcultaDatosPersonales { get; set; }

    /// <summary>Nombres para mostrar de cada tipo de dato.</summary>
    public static readonly Dictionary<string, string> NombresEntidad = new()
    {
        ["Esal"] = "Fundación", ["Causa"] = "Causa", ["Donacion"] = "Donación", ["DonacionEspecie"] = "Donación en especie",
        ["Beneficiario"] = "Beneficiario", ["Publicacion"] = "Publicación del boletín", ["Producto"] = "Producto",
        ["Pedido"] = "Pedido", ["DocumentoTransparencia"] = "Documento de transparencia", ["RendicionCausa"] = "Rendición de cuentas",
        ["DatosDonacion"] = "Datos para donar", ["ConfigPasarela"] = "Pagos en línea", ["EsalModulo"] = "Módulo de la fundación",
        ["NecesidadEspecie"] = "Necesidad en especie", ["Medicamento"] = "Medicamento", ["MovimientoMedicamento"] = "Movimiento de inventario",
        ["EventoAgenda"] = "Cita de la agenda", ["EventoClinico"] = "Registro clínico", ["Apadrinamiento"] = "Apadrinamiento",
        ["PostulacionVoluntario"] = "Postulación de voluntario", ["SolicitudAdopcion"] = "Solicitud de adopción",
        ["Usuario"] = "Cuenta de usuario", ["RolDeUsuario"] = "Rol de usuario", ["FotoCausa"] = "Foto de causa",
        ["FotoEsal"] = "Foto de la fundación", ["FotoProducto"] = "Foto de producto", ["HistorialEstadoBeneficiario"] = "Cambio de estado de beneficiario",
        ["Fondo"] = "Fondo", ["MovimientoFondo"] = "Gasto o traslado de un fondo", ["SalidaEspecie"] = "Uso de una donación en especie",
        ["RegistroCambios"] = "Consulta del registro de cambios"
    };

    public static string NombreEntidad(string entidad) => NombresEntidad.GetValueOrDefault(entidad, entidad);
}

public class RegistroCambioItem
{
    public DateTime Fecha { get; set; }
    public string Usuario { get; set; } = string.Empty;
    public string? Fundacion { get; set; }
    public string Entidad { get; set; } = string.Empty;
    public string EntidadId { get; set; } = string.Empty;
    public AccionAuditoria Accion { get; set; }

    /// <summary>Campo, valor anterior (null al crear) y valor nuevo (null al eliminar).</summary>
    public List<(string Campo, string? Antes, string? Despues)> Campos { get; set; } = new();
}
