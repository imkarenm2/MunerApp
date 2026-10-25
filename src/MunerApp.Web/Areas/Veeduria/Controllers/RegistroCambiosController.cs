using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Seguridad;
using MunerApp.Domain.Constantes;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Areas.Veeduria.Models;
using MunerApp.Web.Servicios;

namespace MunerApp.Web.Areas.Veeduria.Controllers;

/// <summary>
/// Trazabilidad (requerimiento no funcional): consulta de solo lectura del registro de auditoría.
/// El superadministrador lo ve completo; el veedor no ve datos personales de los donantes (como el resto de la veeduría).
/// </summary>
[Area("Veeduria")]
[Authorize(Roles = Roles.Veedor + "," + Roles.SuperAdministrador)]
public class RegistroCambiosController : Controller
{
    private const int PorPagina = 50;

    /// <summary>Datos de personas que el veedor no ve en el registro.</summary>
    private static readonly string[] EntidadesPersonales =
    {
        "Usuario", "RolDeUsuario", "PostulacionVoluntario", "SolicitudAdopcion", "AdoptanteBeneficiario",
        "Apadrinamiento", "ConversacionTienda", "EvaluacionComportamiento"
    };

    /// <summary>Campos con datos de donantes que el veedor no ve aunque el dato sí se muestre.</summary>
    private static readonly string[] CamposPersonales =
        { "DonanteId", "UsuarioId", "PadrinoId", "CompradorId", "Mensaje", "ReferenciaPago", "Soporte", "Direccion", "Telefono", "Correo", "Email" };

    private readonly MunerAppDbContext _db;

    public RegistroCambiosController(MunerAppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index(int? esalId, string? entidad, AccionAuditoria? accion, DateTime? desde, DateTime? hasta, int pagina = 1)
    {
        var ocultar = !User.IsInRole(Roles.SuperAdministrador);
        var consulta = _db.RegistrosAuditoria.AsNoTracking().AsQueryable();
        if (ocultar) consulta = consulta.Where(r => !EntidadesPersonales.Contains(r.Entidad));
        if (esalId is not null) consulta = consulta.Where(r => r.EsalId == esalId);
        if (!string.IsNullOrWhiteSpace(entidad)) consulta = consulta.Where(r => r.Entidad == entidad);
        if (accion is not null) consulta = consulta.Where(r => r.Accion == accion);
        // Las fechas del filtro son días de Colombia; el registro está en UTC
        if (desde is not null) { var inicio = Formatos.Utc(desde.Value.Date); consulta = consulta.Where(r => r.Fecha >= inicio); }
        if (hasta is not null) { var fin = Formatos.Utc(hasta.Value.Date.AddDays(1)); consulta = consulta.Where(r => r.Fecha < fin); }

        // Quien vigila también deja rastro: cada consulta nueva de un veedor queda en el registro (no las de cambio de página)
        if (ocultar && pagina == 1)
            await RegistrarConsultaAsync(esalId, entidad, accion, desde, hasta);

        var total = await consulta.CountAsync();
        var totalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)PorPagina));
        pagina = Math.Clamp(pagina, 1, totalPaginas);
        var registros = await consulta.OrderByDescending(r => r.Fecha).ThenByDescending(r => r.Id)
            .Skip((pagina - 1) * PorPagina).Take(PorPagina).ToListAsync();

        var fundaciones = await _db.Esales.AsNoTracking().OrderBy(e => e.Nombre).Select(e => new { e.Id, e.Nombre }).ToListAsync();
        var nombresEsal = fundaciones.ToDictionary(f => f.Id, f => f.Nombre);

        // Para el veedor solo se muestran por su nombre las personas del equipo de una fundación (o de la plataforma)
        var actores = registros.Where(r => r.UsuarioId != null).Select(r => r.UsuarioId!).Distinct().ToList();
        var equipo = ocultar
            ? (await _db.Users.Where(u => actores.Contains(u.Id) && u.EsalId != null).Select(u => u.Id).ToListAsync()).ToHashSet()
            : actores.ToHashSet();

        return View(new RegistroCambiosViewModel
        {
            EsalId = esalId, Entidad = entidad, Accion = accion, Desde = desde, Hasta = hasta,
            Fundaciones = fundaciones.Select(f => (f.Id, f.Nombre)).ToList(),
            Entidades = await _db.RegistrosAuditoria.AsNoTracking()
                .Where(r => !ocultar || !EntidadesPersonales.Contains(r.Entidad))
                .Select(r => r.Entidad).Distinct().OrderBy(e => e).ToListAsync(),
            Pagina = pagina, TotalPaginas = totalPaginas, Total = total,
            OcultaDatosPersonales = ocultar,
            Registros = registros.Select(r => new RegistroCambioItem
            {
                Fecha = r.Fecha,
                Usuario = r.UsuarioId is null ? "Sistema"
                    : equipo.Contains(r.UsuarioId) || r.Accion == AccionAuditoria.Consultado ? r.UsuarioNombre : "Un donante",
                Fundacion = r.EsalId is int id ? nombresEsal.GetValueOrDefault(id) : null,
                Entidad = r.Entidad,
                EntidadId = r.EntidadId,
                Accion = r.Accion,
                Campos = Campos(r.Cambios, r.Accion, ocultar)
            }).ToList()
        });
    }

    private async Task RegistrarConsultaAsync(int? esalId, string? entidad, AccionAuditoria? accion, DateTime? desde, DateTime? hasta)
    {
        var filtros = new Dictionary<string, string?>
        {
            ["Fundacion"] = esalId?.ToString(),
            ["Tipo"] = entidad,
            ["Accion"] = accion?.ToString(),
            ["Desde"] = desde?.ToString("yyyy-MM-dd"),
            ["Hasta"] = hasta?.ToString("yyyy-MM-dd")
        }.Where(f => f.Value is not null).ToDictionary(f => f.Key, f => f.Value);

        _db.RegistrosAuditoria.Add(new RegistroAuditoria
        {
            UsuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            UsuarioNombre = "Veedor: " + (User.FindFirst(MunerAppClaims.NombreCompleto)?.Value ?? User.Identity?.Name ?? ""),
            EsalId = esalId,
            Entidad = "RegistroCambios",
            EntidadId = "consulta",
            Accion = AccionAuditoria.Consultado,
            Cambios = JsonSerializer.Serialize(filtros)
        });
        await _db.SaveChangesAsync();
    }

    /// <summary>Convierte el JSON de cambios en filas de campo, antes y después.</summary>
    private static List<(string, string?, string?)> Campos(string json, AccionAuditoria accion, bool ocultar)
    {
        var campos = new List<(string, string?, string?)>();
        using var documento = JsonDocument.Parse(json);
        foreach (var campo in documento.RootElement.EnumerateObject())
        {
            if (ocultar && CamposPersonales.Any(c => campo.Name.Contains(c, StringComparison.OrdinalIgnoreCase))) continue;
            if (accion == AccionAuditoria.Modificado && campo.Value.ValueKind == JsonValueKind.Array && campo.Value.GetArrayLength() == 2)
                campos.Add((campo.Name, Texto(campo.Value[0]), Texto(campo.Value[1])));
            else if (accion == AccionAuditoria.Eliminado)
                campos.Add((campo.Name, Texto(campo.Value), null));
            else
                campos.Add((campo.Name, null, Texto(campo.Value)));
        }
        return campos;
    }

    private static string? Texto(JsonElement valor) => valor.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.String => valor.GetString(),
        _ => valor.GetRawText()
    };
}
