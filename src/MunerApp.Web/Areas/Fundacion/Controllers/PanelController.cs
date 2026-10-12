using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Application.Seguridad;
using MunerApp.Domain.Constantes;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Areas.Fundacion.Models;
using MunerApp.Web.Seguridad;
using MunerApp.Web.Servicios;

namespace MunerApp.Web.Areas.Fundacion.Controllers;

/// <summary>
/// Inicio del panel de la fundación: un tablero con cuántos gatos hay, lo que está pendiente para hoy,
/// cómo va el recaudo y lo último que pasó. Cada rol ve solo lo que le corresponde.
/// </summary>
[Area("Fundacion")]
[Authorize(Roles = Roles.AdministradorESAL + "," + Roles.Voluntario)]
public class PanelController : Controller
{
    private readonly MunerAppDbContext _db;
    private readonly IEsalActual _esalActual;
    private readonly IModuloService _modulos;
    private readonly IAlmacenamientoArchivos _archivos;

    public PanelController(MunerAppDbContext db, IEsalActual esalActual, IModuloService modulos, IAlmacenamientoArchivos archivos)
    {
        _db = db;
        _esalActual = esalActual;
        _modulos = modulos;
        _archivos = archivos;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (_esalActual.EsalId is not int esalId)
            return RedirectToAction("Index", "Home", new { area = "" });

        var esal = await _db.Esales.AsNoTracking().FirstOrDefaultAsync(e => e.Id == esalId);
        if (esal is null) return NotFound();

        var esAdmin = User.IsInRole(Roles.AdministradorESAL);
        var esPrincipal = Politicas.EsAdminPrincipal(User);
        var clinico = Politicas.TieneAccesoClinico(User);
        var moduloBeneficiarios = await _modulos.EstaActivoAsync(esalId, CodigosModulo.Beneficiarios);

        var hoy = Formatos.Hoy();
        var inicioMesUtc = Formatos.AUtc(new DateTime(hoy.Year, hoy.Month, 1));

        var portada = await _db.FotosEsal.AsNoTracking().OrderBy(f => f.Orden).Select(f => f.Ruta).FirstOrDefaultAsync();

        var modelo = new PanelViewModel
        {
            NombreEsal = esal.Nombre,
            NombreUsuario = User.FindFirst(MunerAppClaims.NombreCompleto)?.Value?.Split(' ')[0] ?? "",
            RolTexto = esAdmin
                ? (esPrincipal ? "Administrador principal" : "Administrador de consulta")
                : (Politicas.EsVoluntarioSalud(User) ? "Voluntario de salud" : "Voluntario"),
            Hoy = hoy,
            EsAdmin = esAdmin,
            EsAdminPrincipal = esPrincipal,
            AccesoClinico = clinico,
            ModuloBeneficiarios = moduloBeneficiarios,
            Slug = esal.Slug,
            FotoPortada = portada is null ? null : _archivos.UrlPublica(portada)
        };

        var tareas = new List<TareaPanel>();
        var actividad = new List<ActividadPanel>();

        // ---------- Gatos ----------
        if (moduloBeneficiarios)
        {
            var porEstado = await _db.Beneficiarios.AsNoTracking()
                .GroupBy(b => b.Estado)
                .Select(g => new { g.Key, Cantidad = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Cantidad);
            int Cuantos(EstadoBeneficiario e) => porEstado.TryGetValue(e, out var c) ? c : 0;

            modelo.EnLaFundacion = Cuantos(EstadoBeneficiario.EnLaFundacion);
            modelo.EnTratamiento = Cuantos(EstadoBeneficiario.EnTratamiento);
            modelo.Adoptables = Cuantos(EstadoBeneficiario.Adoptable);
            modelo.GatosEnCasa = modelo.EnLaFundacion + modelo.EnTratamiento + modelo.Adoptables;
            modelo.HogaresTotal = Cuantos(EstadoBeneficiario.Adoptado) + Cuantos(EstadoBeneficiario.EncontroSuHogar);

            var hogares = new[] { EstadoBeneficiario.Adoptado, EstadoBeneficiario.EncontroSuHogar };
            modelo.HogaresEsteMes = await _db.HistorialEstadosBeneficiario.AsNoTracking()
                .Where(h => hogares.Contains(h.Estado) && h.Fecha >= inicioMesUtc)
                .Select(h => h.BeneficiarioId).Distinct().CountAsync();

            var enCasa = ReglasBeneficiario.EnCasa;
            modelo.Gatos = (await _db.Beneficiarios.AsNoTracking()
                .Where(b => enCasa.Contains(b.Estado) && b.FotoRuta != null)
                .OrderByDescending(b => b.FechaRescate)
                .Take(7)
                .Select(b => new { b.Id, b.Nombre })
                .ToListAsync())
                .Select(b => new GatoMini(b.Id, b.Nombre)).ToList();

            if (clinico)
            {
                var limiteVacuna = hoy.AddDays(-ReglasBeneficiario.DiasVacuna);
                var limiteDesparasitacion = hoy.AddDays(-ReglasBeneficiario.DiasDesparasitacion);
                var gatosEnCasa = _db.Beneficiarios.AsNoTracking().Where(b => enCasa.Contains(b.Estado));

                var sinExamen = await gatosEnCasa.CountAsync(b => b.FechaExamenIngreso == null);
                var porVacunar = await gatosEnCasa.CountAsync(b => !_db.EventosClinicos
                    .Any(e => e.BeneficiarioId == b.Id && e.Tipo == TipoEventoClinico.Vacuna && e.Fecha >= limiteVacuna));
                var porDesparasitar = await gatosEnCasa.CountAsync(b => !_db.EventosClinicos
                    .Any(e => e.BeneficiarioId == b.Id && e.Tipo == TipoEventoClinico.Desparasitacion && e.Fecha >= limiteDesparasitacion));

                if (modelo.EnTratamiento > 0)
                    tareas.Add(new TareaPanel("bi-bandaid", "alerta",
                        Gatos(modelo.EnTratamiento) + " en tratamiento", "Revisen cómo siguen y registren los controles.",
                        Url.Action("Index", "Beneficiarios", new { estado = EstadoBeneficiario.EnTratamiento })!));
                if (porVacunar > 0)
                    tareas.Add(new TareaPanel("bi-shield-plus", "alerta",
                        Gatos(porVacunar) + " por vacunar", "Sin vacuna registrada en el último año.",
                        Url.Action("Index", "Beneficiarios", new { pendiente = PendienteBeneficiario.Vacuna })!));
                if (porDesparasitar > 0)
                    tareas.Add(new TareaPanel("bi-capsule", "alerta",
                        Gatos(porDesparasitar) + " por desparasitar", $"Sin desparasitación en los últimos {ReglasBeneficiario.DiasDesparasitacion} días.",
                        Url.Action("Index", "Beneficiarios", new { pendiente = PendienteBeneficiario.Desparasitacion })!));
                if (sinExamen > 0)
                    tareas.Add(new TareaPanel("bi-clipboard2-pulse", "vacio",
                        Gatos(sinExamen) + " sin examen de ingreso", "Completen la hoja de vida con el examen del día que llegaron.",
                        Url.Action("Index", "Beneficiarios", new { pendiente = PendienteBeneficiario.Examen })!));

                var eventos = await _db.EventosClinicos.AsNoTracking()
                    .OrderByDescending(e => e.FechaRegistro).Take(5)
                    .Select(e => new { e.FechaRegistro, e.Tipo, e.BeneficiarioId, Gato = e.Beneficiario!.Nombre })
                    .ToListAsync();
                actividad.AddRange(eventos.Select(e => new ActividadPanel(e.FechaRegistro, "bi-heart-pulse",
                    $"Historia clínica de {e.Gato}: {Textos.De(e.Tipo).ToLower()}",
                    Url.Action("Index", "HistoriaClinica", new { id = e.BeneficiarioId }))));
            }

            if (esPrincipal)
            {
                var sinAdoptante = await _db.Beneficiarios.CountAsync(b => b.Estado == EstadoBeneficiario.Adoptado && b.Adoptante == null);
                if (sinAdoptante > 0)
                    tareas.Add(new TareaPanel("bi-person-vcard", "vacio",
                        Gatos(sinAdoptante) + (sinAdoptante == 1 ? " adoptado sin datos del adoptante" : " adoptados sin datos del adoptante"),
                        "Registren quién los adoptó para hacerles seguimiento.",
                        Url.Action("Index", "Beneficiarios", new { estado = EstadoBeneficiario.Adoptado })!));
            }

            var nuevos = await _db.Beneficiarios.AsNoTracking()
                .OrderByDescending(b => b.FechaRegistro).Take(4)
                .Select(b => new { b.Id, b.Nombre, b.FechaRegistro }).ToListAsync();
            actividad.AddRange(nuevos.Select(b => new ActividadPanel(b.FechaRegistro, "bi-balloon-heart",
                $"Llegó {b.Nombre} a la fundación", Url.Action("Detalle", "Beneficiarios", new { id = b.Id }))));

            var cambios = await _db.HistorialEstadosBeneficiario.AsNoTracking()
                .Where(h => h.Nota == null || !h.Nota.StartsWith("Registro"))
                .OrderByDescending(h => h.Fecha).Take(5)
                .Select(h => new { h.Fecha, h.Estado, h.BeneficiarioId, Gato = h.Beneficiario!.Nombre }).ToListAsync();
            actividad.AddRange(cambios.Select(h => new ActividadPanel(h.Fecha, IconoEstado(h.Estado),
                $"{h.Gato} pasó a «{Textos.De(h.Estado)}»", Url.Action("Detalle", "Beneficiarios", new { id = h.BeneficiarioId }))));
        }

        // ---------- Recaudo y comunidad (administradores) ----------
        if (esAdmin)
        {
            var confirmadasMes = _db.Donaciones.AsNoTracking()
                .Where(d => d.Estado == EstadoDonacion.Confirmada && d.FechaRevision >= inicioMesUtc);
            modelo.DonadoEsteMes = await confirmadasMes.SumAsync(d => (decimal?)d.Valor) ?? 0;
            modelo.DonacionesEsteMes = await confirmadasMes.CountAsync();

            var apadrinamientos = _db.Apadrinamientos.AsNoTracking().Where(a => a.Estado == EstadoApadrinamiento.Activo);
            modelo.PadrinosActivos = await apadrinamientos.Select(a => a.PadrinoId).Distinct().CountAsync();
            modelo.AporteMensualPadrinos = await apadrinamientos.SumAsync(a => (decimal?)a.ValorMensual) ?? 0;

            var donacionesPendientes = await _db.Donaciones.CountAsync(d => d.Estado == EstadoDonacion.Pendiente);
            var postulacionesPendientes = await _db.PostulacionesVoluntario.CountAsync(p => p.Estado == EstadoPostulacion.Pendiente);

            if (donacionesPendientes > 0)
                tareas.Insert(0, new TareaPanel("bi-cash-coin", "error",
                    donacionesPendientes == 1 ? "1 donación por confirmar" : $"{donacionesPendientes} donaciones por confirmar",
                    "Los donantes esperan saber que su aporte llegó.",
                    Url.Action("Index", "Donaciones")!));
            if (postulacionesPendientes > 0)
                tareas.Add(new TareaPanel("bi-person-raised-hand", "exito",
                    postulacionesPendientes == 1 ? "1 persona quiere ser voluntaria" : $"{postulacionesPendientes} personas quieren ser voluntarias",
                    "Revisen sus postulaciones y respóndanles.",
                    Url.Action("Index", "Postulaciones")!));

            // Causas activas con su avance
            var causas = await _db.Causas.AsNoTracking()
                .Where(c => c.Estado == EstadoCausa.Activa && c.FechaLimite >= hoy)
                .OrderBy(c => c.FechaLimite)
                .Select(c => new
                {
                    c.Id, c.Titulo, c.Meta, c.FechaLimite,
                    Recaudado = _db.Donaciones.Where(d => d.CausaId == c.Id && d.Estado == EstadoDonacion.Confirmada).Sum(d => (decimal?)d.Valor) ?? 0
                })
                .ToListAsync();
            modelo.Causas = causas
                .Where(c => c.Recaudado < c.Meta)
                .Take(3)
                .Select(c => new CausaPanel(c.Id, c.Titulo, c.Meta, c.Recaudado, Math.Max(0, (c.FechaLimite.Date - hoy).Days)))
                .ToList();

            var donaciones = await _db.Donaciones.AsNoTracking()
                .OrderByDescending(d => d.FechaReporte).Take(5)
                .Select(d => new { d.Id, d.Valor, d.FechaReporte, d.Estado }).ToListAsync();
            actividad.AddRange(donaciones.Select(d => new ActividadPanel(d.FechaReporte, "bi-cash-coin",
                $"Donación de {Formatos.Pesos(d.Valor)}" + (d.Estado == EstadoDonacion.Pendiente ? " (por confirmar)" : ""),
                Url.Action("Detalle", "Donaciones", new { id = d.Id }))));
        }

        // ---------- Perfil público (administrador principal) ----------
        if (esPrincipal)
        {
            var causasRechazadas = await _db.Causas.CountAsync(c => c.Estado == EstadoCausa.Rechazada);
            if (causasRechazadas > 0)
                tareas.Add(new TareaPanel("bi-arrow-counterclockwise", "alerta",
                    causasRechazadas == 1 ? "1 causa necesita cambios" : $"{causasRechazadas} causas necesitan cambios",
                    "El equipo de MunerApp dejó observaciones antes de publicarlas.",
                    Url.Action("Index", "Causas")!));

            if (!ReglasPublicacion.PerfilMinimo(esal))
                tareas.Add(new TareaPanel("bi-eye-slash", "error",
                    "La fundación no aparece en el directorio",
                    "Completen la descripción corta y la misión del perfil.",
                    Url.Action("Index", "Perfil")!));
            if (!await _db.DatosDonacion.AnyAsync())
                tareas.Add(new TareaPanel("bi-bank", "error",
                    "Faltan los datos para donar",
                    "Sin una cuenta o llave registrada nadie puede donar.",
                    Url.Action("Index", "DatosDonacion")!));
            else
            {
                var completo = PorcentajePerfil(esal, portada is not null);
                if (completo < 100)
                    tareas.Add(new TareaPanel("bi-building", "vacio",
                        $"Perfil al {completo} %", "Un perfil completo genera más confianza en los donantes.",
                        Url.Action("Index", "Perfil")!));
            }
        }

        modelo.Tareas = tareas;
        modelo.Actividad = actividad.OrderByDescending(a => a.FechaUtc).Take(8).ToList();
        return View(modelo);
    }

    private static string Gatos(int n) => n == 1 ? "1 gato" : $"{n} gatos";

    private static string IconoEstado(EstadoBeneficiario e) => e switch
    {
        EstadoBeneficiario.Adoptado or EstadoBeneficiario.EncontroSuHogar => "bi-house-heart",
        EstadoBeneficiario.EnTratamiento => "bi-bandaid",
        EstadoBeneficiario.Adoptable => "bi-stars",
        EstadoBeneficiario.Fallecido => "bi-flower1",
        EstadoBeneficiario.Liberado => "bi-tree",
        _ => "bi-arrow-repeat"
    };

    /// <summary>Qué tan completo está el perfil público (para animar a la fundación a terminarlo).</summary>
    private static int PorcentajePerfil(Domain.Entities.Esal e, bool tieneFotos)
    {
        var campos = new[]
        {
            !string.IsNullOrWhiteSpace(e.DescripcionCorta), !string.IsNullOrWhiteSpace(e.Historia),
            !string.IsNullOrWhiteSpace(e.Mision), !string.IsNullOrWhiteSpace(e.Vision),
            !string.IsNullOrWhiteSpace(e.Ciudad), !string.IsNullOrWhiteSpace(e.LogoRuta), tieneFotos
        };
        return (int)Math.Round(100.0 * campos.Count(c => c) / campos.Length);
    }
}
