using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Seguridad;
using MunerApp.Domain.Constantes;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Areas.Fundacion.Models;
using MunerApp.Web.Areas.Plataforma.Models;
using MunerApp.Web.Servicios;

namespace MunerApp.Web.Areas.Plataforma.Controllers;

/// <summary>
/// Inicio del superadministrador: cómo va la plataforma en general, qué está pendiente de su parte
/// (sobre todo aprobar causas) y cómo va cada fundación. Son cifras para hacer seguimiento, sin datos personales.
/// </summary>
[Area("Plataforma")]
[Authorize(Roles = Roles.SuperAdministrador)]
public class ResumenController : Controller
{
    private readonly MunerAppDbContext _db;

    public ResumenController(MunerAppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var hoy = Formatos.Hoy();
        var inicioMesUtc = Formatos.AUtc(new DateTime(hoy.Year, hoy.Month, 1));
        var enCasa = ReglasBeneficiario.EnCasa;
        var hogares = new[] { EstadoBeneficiario.Adoptado, EstadoBeneficiario.EncontroSuHogar };

        var donantesQuery = from u in _db.Users
                            join ur in _db.UserRoles on u.Id equals ur.UserId
                            join r in _db.Roles on ur.RoleId equals r.Id
                            where r.Name == Roles.Donante
                            select u;
        var voluntariosQuery = from u in _db.Users
                               join ur in _db.UserRoles on u.Id equals ur.UserId
                               join r in _db.Roles on ur.RoleId equals r.Id
                               where r.Name == Roles.Voluntario && u.Activo
                               select u;

        var confirmadas = _db.Donaciones.AsNoTracking().Where(d => d.Estado == EstadoDonacion.Confirmada);
        var confirmadasMes = confirmadas.Where(d => d.FechaRevision >= inicioMesUtc);
        var apadrinamientos = _db.Apadrinamientos.AsNoTracking().Where(a => a.Estado == EstadoApadrinamiento.Activo);

        var modelo = new ResumenPlataformaViewModel
        {
            NombreUsuario = User.FindFirst(MunerAppClaims.NombreCompleto)?.Value?.Split(' ')[0] ?? "",
            Hoy = hoy,
            FundacionesActivas = await _db.Esales.CountAsync(e => e.Activa),
            FundacionesInactivas = await _db.Esales.CountAsync(e => !e.Activa),
            Donantes = await donantesQuery.CountAsync(),
            DonantesNuevosMes = await donantesQuery.CountAsync(u => u.FechaRegistro >= inicioMesUtc),
            DonadoTotal = await confirmadas.SumAsync(d => (decimal?)d.Valor) ?? 0,
            DonadoMes = await confirmadasMes.SumAsync(d => (decimal?)d.Valor) ?? 0,
            DonacionesMes = await confirmadasMes.CountAsync(),
            GatosEnCasa = await _db.Beneficiarios.CountAsync(b => enCasa.Contains(b.Estado)),
            HogaresTotal = await _db.Beneficiarios.CountAsync(b => hogares.Contains(b.Estado)),
            HogaresMes = await _db.HistorialEstadosBeneficiario
                .Where(h => hogares.Contains(h.Estado) && h.Fecha >= inicioMesUtc)
                .Select(h => h.BeneficiarioId).Distinct().CountAsync(),
            PadrinosActivos = await apadrinamientos.Select(a => a.PadrinoId).Distinct().CountAsync(),
            AporteMensualPadrinos = await apadrinamientos.SumAsync(a => (decimal?)a.ValorMensual) ?? 0,
            VoluntariosActivos = await voluntariosQuery.CountAsync(),
            CausasActivas = await _db.Causas.CountAsync(c => c.Estado == EstadoCausa.Activa && c.FechaLimite >= hoy)
        };

        // ---------- Causas por aprobar ----------
        modelo.CausasPorAprobar = (await _db.Causas.AsNoTracking()
            .Where(c => c.Estado == EstadoCausa.PorAprobar)
            .OrderBy(c => c.FechaCreacion)
            .Take(5)
            .Select(c => new { c.Id, c.Titulo, Fundacion = c.Esal!.Nombre, c.Meta, c.FechaCreacion, Corregida = c.FechaRevision != null })
            .ToListAsync())
            .Select(c => new CausaPorAprobarItem(c.Id, c.Titulo, c.Fundacion, c.Meta, c.FechaCreacion, c.Corregida))
            .ToList();

        // ---------- Cada fundación ----------
        var esales = await _db.Esales.AsNoTracking().OrderBy(e => e.Nombre).ToListAsync();
        var gatos = await _db.Beneficiarios.Where(b => enCasa.Contains(b.Estado))
            .GroupBy(b => b.EsalId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        var donadoMes = await confirmadasMes.GroupBy(d => d.EsalId)
            .Select(g => new { g.Key, V = g.Sum(d => d.Valor) }).ToDictionaryAsync(x => x.Key, x => x.V);
        var pendientes = await _db.Donaciones.Where(d => d.Estado == EstadoDonacion.Pendiente)
            .GroupBy(d => d.EsalId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        var causas = await _db.Causas.Where(c => c.Estado == EstadoCausa.Activa && c.FechaLimite >= hoy)
            .GroupBy(c => c.EsalId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        var padrinos = await apadrinamientos.GroupBy(a => a.EsalId)
            .Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        var conDatos = (await _db.DatosDonacion.Select(d => d.EsalId).Distinct().ToListAsync()).ToHashSet();

        modelo.Fundaciones = esales.Select(e => new FundacionResumenItem
        {
            Id = e.Id,
            Nombre = e.Nombre,
            Slug = e.Slug,
            Ciudad = e.Ciudad,
            Activa = e.Activa,
            EnDirectorio = e.Activa && ReglasPublicacion.PerfilMinimo(e),
            Gatos = gatos.GetValueOrDefault(e.Id),
            DonadoMes = donadoMes.GetValueOrDefault(e.Id),
            DonacionesPendientes = pendientes.GetValueOrDefault(e.Id),
            CausasActivas = causas.GetValueOrDefault(e.Id),
            Padrinos = padrinos.GetValueOrDefault(e.Id)
        }).ToList();

        // ---------- Pendientes del superadministrador ----------
        var tareas = new List<TareaPanel>();
        var porAprobar = await _db.Causas.CountAsync(c => c.Estado == EstadoCausa.PorAprobar);
        if (porAprobar > 0)
            tareas.Add(new TareaPanel("bi-bullseye", "error",
                porAprobar == 1 ? "1 causa por aprobar" : $"{porAprobar} causas por aprobar",
                "Las fundaciones esperan tu revisión para publicarlas.",
                Url.Action("Index", "Causas")!));

        var fueraDirectorio = modelo.Fundaciones.Count(f => f.Activa && !f.EnDirectorio);
        if (fueraDirectorio > 0)
            tareas.Add(new TareaPanel("bi-eye-slash", "alerta",
                fueraDirectorio == 1 ? "1 fundación no aparece en el directorio" : $"{fueraDirectorio} fundaciones no aparecen en el directorio",
                "Les falta la descripción corta o la misión del perfil.",
                Url.Action("Index", "Esales")!));

        var sinDatos = esales.Count(e => e.Activa && !conDatos.Contains(e.Id));
        if (sinDatos > 0)
            tareas.Add(new TareaPanel("bi-bank", "alerta",
                sinDatos == 1 ? "1 fundación sin datos para donar" : $"{sinDatos} fundaciones sin datos para donar",
                "Nadie puede donarles hasta que registren una cuenta o llave.",
                Url.Action("Index", "Esales")!));

        var hace7Dias = DateTime.UtcNow.AddDays(-7);
        var demoradas = await _db.Donaciones.CountAsync(d => d.Estado == EstadoDonacion.Pendiente && d.FechaReporte < hace7Dias);
        if (demoradas > 0)
            tareas.Add(new TareaPanel("bi-hourglass-split", "vacio",
                demoradas == 1 ? "1 donación lleva más de una semana sin revisar" : $"{demoradas} donaciones llevan más de una semana sin revisar",
                "Puede valer la pena recordarle a la fundación.",
                Url.Action("Donaciones", "Informes")!));

        modelo.Tareas = tareas;
        return View(modelo);
    }
}
