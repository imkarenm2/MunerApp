using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Application.Seguridad;
using MunerApp.Domain.Constantes;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Identity;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Areas.Fundacion.Models;
using MunerApp.Web.Seguridad;
using MunerApp.Web.Validacion;

namespace MunerApp.Web.Areas.Fundacion.Controllers;

/// <summary>
/// HU-035: la fundación consulta las postulaciones de voluntarios.
/// HU-036: el administrador principal las aprueba (la persona obtiene el rol de voluntario general o de salud
/// en la fundación), las rechaza (conserva su rol de donante) o desvincula a un voluntario activo
/// (pierde el acceso a los módulos internos de inmediato).
/// </summary>
[Area("Fundacion")]
[Authorize(Roles = Roles.AdministradorESAL)]
public class PostulacionesController : Controller
{
    private readonly MunerAppDbContext _db;
    private readonly IAlmacenamientoArchivos _archivos;
    private readonly UserManager<Usuario> _userManager;
    private readonly INotificacionService _notificaciones;
    private readonly IEsalActual _esalActual;

    public PostulacionesController(MunerAppDbContext db, IAlmacenamientoArchivos archivos, UserManager<Usuario> userManager,
        INotificacionService notificaciones, IEsalActual esalActual)
    {
        _db = db;
        _archivos = archivos;
        _userManager = userManager;
        _notificaciones = notificaciones;
        _esalActual = esalActual;
    }

    private int EsalId => _esalActual.EsalId ?? throw new InvalidOperationException("El usuario no pertenece a una ESAL.");

    /// <summary>
    /// El área va explícita en los enlaces: sin ella, "Index" se confunde con la acción pública
    /// /mis-postulaciones del controlador del mismo nombre.
    /// </summary>
    private const string AreaFundacion = "Fundacion";

    [HttpGet]
    public async Task<IActionResult> Index(EstadoPostulacion estado = EstadoPostulacion.Pendiente)
    {
        var pestana = PostulacionesEsalViewModel.Pestanas.FirstOrDefault(p => p.Estados.Contains(estado));
        var estados = pestana.Estados ?? PostulacionesEsalViewModel.Pestanas[0].Estados;

        var conteos = await _db.PostulacionesVoluntario.AsNoTracking()
            .GroupBy(p => p.Estado)
            .Select(g => new { g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Cantidad);

        var postulaciones = await (from p in _db.PostulacionesVoluntario.AsNoTracking()
                                   join u in _db.Users on p.UsuarioId equals u.Id
                                   where estados.Contains(p.Estado)
                                   orderby p.FechaPostulacion descending
                                   select new PostulacionEsalItem
                                   {
                                       Id = p.Id,
                                       Nombre = u.NombreCompleto,
                                       Correo = u.Email ?? "",
                                       Telefono = p.Telefono,
                                       Tipo = p.Tipo,
                                       Disponibilidad = p.Disponibilidad,
                                       Motivacion = p.Motivacion,
                                       Institucion = p.Institucion,
                                       Programa = p.Programa,
                                       Semestre = p.Semestre,
                                       TieneSoporte = p.SoporteAcademicoRuta != null,
                                       Estado = p.Estado,
                                       Fecha = p.FechaPostulacion,
                                       MotivoRechazo = p.MotivoRechazo,
                                       FechaRespuesta = p.FechaRespuesta,
                                       FechaRetiro = p.FechaRetiro
                                   }).Take(200).ToListAsync();

        return View(new PostulacionesEsalViewModel
        {
            Estado = estados[0],
            Conteos = conteos,
            Postulaciones = postulaciones,
            PuedeGestionar = User.HasClaim(MunerAppClaims.Perfil, Perfiles.Principal)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Soporte(int id)
    {
        var p = await _db.PostulacionesVoluntario.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (p?.SoporteAcademicoRuta is null) return NotFound();
        var stream = await _archivos.AbrirAsync(p.SoporteAcademicoRuta);
        return stream is null ? NotFound() : File(stream, ValidadorArchivos.ContentTypeDe(p.SoporteAcademicoRuta));
    }

    // ---------- HU-036 escenario 1: aprobar ----------

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> Aprobar(int id)
    {
        var (p, usuario, error) = await BuscarAsync(id, EstadoPostulacion.Pendiente);
        if (error is not null) return error;

        // Una cuenta pertenece a una sola fundación: si ya es parte de otra, no se puede aprobar aquí
        if (usuario!.EsalId is not null && usuario.EsalId != EsalId)
        {
            TempData["Error"] = $"{usuario.NombreCompleto} ya hace parte del equipo de otra fundación. Para unirse a la suya debe desvincularse primero de esa.";
            return RedirectToAction(nameof(Index), new { area = AreaFundacion });
        }
        if (await _userManager.IsInRoleAsync(usuario, Roles.AdministradorESAL) || await _userManager.IsInRoleAsync(usuario, Roles.SuperAdministrador))
        {
            TempData["Error"] = $"{usuario.NombreCompleto} es administrador: no se puede aprobar como voluntario.";
            return RedirectToAction(nameof(Index), new { area = AreaFundacion });
        }

        // El perfil depende de cómo se postuló: los practicantes de salud acceden a la información clínica
        var perfil = p!.Tipo == TipoPostulacion.PracticanteSalud ? Perfiles.Salud : Perfiles.General;
        usuario.EsalId = EsalId;
        usuario.Perfil = perfil;
        var resultado = await _userManager.UpdateAsync(usuario);
        if (resultado.Succeeded && !await _userManager.IsInRoleAsync(usuario, Roles.Voluntario))
            resultado = await _userManager.AddToRoleAsync(usuario, Roles.Voluntario);
        if (!resultado.Succeeded)
        {
            TempData["Error"] = "No se pudo asignar el rol de voluntario. Intenta de nuevo.";
            return RedirectToAction(nameof(Index), new { area = AreaFundacion });
        }
        // No se cambia su sello de seguridad: en máximo un minuto su sesión se actualiza con el nuevo rol (HU-008)

        p.Estado = EstadoPostulacion.Aprobada;
        MarcarRevisada(p);
        var rol = perfil == Perfiles.Salud ? "voluntario de salud" : "voluntario";
        _notificaciones.Agregar(usuario.Id, "¡Ya eres voluntario!",
            $"{p.Esal!.Nombre} aprobó tu postulación. Ya eres {rol} de la fundación y puedes entrar a su panel.",
            "/Fundacion", "bi-person-check");
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"Aprobaste a {usuario.NombreCompleto}. Ahora es {rol} de la fundación y aparece en Equipo.";
        return RedirectToAction(nameof(Index), new { area = AreaFundacion, estado = EstadoPostulacion.Aprobada });
    }

    // ---------- HU-036 escenario 2: rechazar ----------

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> Rechazar(int id, string? motivo)
    {
        var (p, usuario, error) = await BuscarAsync(id, EstadoPostulacion.Pendiente);
        if (error is not null) return error;

        motivo = motivo?.Trim();
        if (string.IsNullOrEmpty(motivo) || motivo.Length < 10)
        {
            TempData["Error"] = "Escribe el motivo (mínimo 10 caracteres) para que la persona sepa por qué.";
            return RedirectToAction(nameof(Index), new { area = AreaFundacion });
        }

        // Conserva su rol de donante: no se toca su cuenta
        p!.Estado = EstadoPostulacion.Rechazada;
        p.MotivoRechazo = motivo.Length > 300 ? motivo[..300] : motivo;
        MarcarRevisada(p);
        _notificaciones.Agregar(usuario!.Id, "Tu postulación de voluntario no fue aprobada",
            $"{p.Esal!.Nombre} no aprobó tu postulación por ahora. Motivo: {p.MotivoRechazo}",
            "/mis-postulaciones", "bi-x-circle");
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"Rechazaste la postulación de {usuario.NombreCompleto}. Verá el motivo.";
        return RedirectToAction(nameof(Index), new { area = AreaFundacion });
    }

    // ---------- HU-036 escenario 3: desvincular a un voluntario activo ----------

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> Desvincular(int id)
    {
        var (p, usuario, error) = await BuscarAsync(id, EstadoPostulacion.Aprobada);
        if (error is not null) return error;

        // Pierde el rol y la fundación; vuelve a ser donante
        if (usuario!.EsalId == EsalId)
        {
            usuario.EsalId = null;
            usuario.Perfil = null;
            await _userManager.UpdateAsync(usuario);
            if (await _userManager.IsInRoleAsync(usuario, Roles.Voluntario))
                await _userManager.RemoveFromRoleAsync(usuario, Roles.Voluntario);
            if (!await _userManager.IsInRoleAsync(usuario, Roles.Donante))
                await _userManager.AddToRoleAsync(usuario, Roles.Donante);
            // Cierra su sesión para que pierda el acceso a los módulos internos de inmediato
            await _userManager.UpdateSecurityStampAsync(usuario);
        }

        p!.Estado = EstadoPostulacion.Retirada;
        p.FechaRetiro = DateTime.UtcNow;
        p.RevisadoPorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        _notificaciones.Agregar(usuario.Id, "Ya no haces parte del equipo de voluntarios",
            $"{p.Esal!.Nombre} terminó tu vinculación como voluntario. Gracias por tu tiempo; puedes seguir apoyándola de otras formas.",
            "/mis-postulaciones", "bi-person-dash");
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"Desvinculaste a {usuario.NombreCompleto}. Ya no tiene acceso al panel de la fundación.";
        return RedirectToAction(nameof(Index), new { area = AreaFundacion, estado = EstadoPostulacion.Retirada });
    }

    /// <summary>La postulación de la fundación (filtro por ESAL) en el estado esperado, con la cuenta de la persona.</summary>
    private async Task<(PostulacionVoluntario? Postulacion, Usuario? Usuario, IActionResult? Error)> BuscarAsync(int id, EstadoPostulacion esperado)
    {
        var p = await _db.PostulacionesVoluntario.Include(x => x.Esal).FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return (null, null, NotFound());
        if (p.Estado != esperado)
        {
            TempData["Error"] = $"Esta postulación ya está en estado \"{Textos.De(p.Estado)}\".";
            return (p, null, RedirectToAction(nameof(Index), new { area = AreaFundacion, estado = p.Estado }));
        }
        var usuario = await _userManager.FindByIdAsync(p.UsuarioId);
        if (usuario is null) return (p, null, NotFound());
        return (p, usuario, null);
    }

    private void MarcarRevisada(PostulacionVoluntario p)
    {
        p.FechaRespuesta = DateTime.UtcNow;
        p.RevisadoPorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
