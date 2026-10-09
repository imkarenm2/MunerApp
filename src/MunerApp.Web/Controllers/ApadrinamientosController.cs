using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Application.Seguridad;
using MunerApp.Domain.Constantes;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Models.Publico;
using MunerApp.Web.Seguridad;
using MunerApp.Web.Servicios;

namespace MunerApp.Web.Controllers;

/// <summary>
/// HU-021: el donante apadrina a un beneficiario con un aporte mensual y consulta "Mis apadrinamientos".
/// El aporte no se cobra solo: el padrino transfiere a la cuenta oficial y reporta su aporte, que sigue el
/// mismo flujo de confirmación de las donaciones (<see cref="DonacionesController"/>).
/// Los apadrinamientos de un donante pueden ser de varias fundaciones: se consultan con IgnoreQueryFilters
/// y siempre filtrando por el usuario autenticado.
/// </summary>
[Authorize]
public class ApadrinamientosController : Controller
{
    private const decimal ValorMinimo = 5_000;
    private const decimal ValorMaximo = 5_000_000;

    private readonly MunerAppDbContext _db;
    private readonly IAlmacenamientoArchivos _archivos;
    private readonly IModuloService _modulos;
    private readonly INotificacionService _notificaciones;

    public ApadrinamientosController(MunerAppDbContext db, IAlmacenamientoArchivos archivos,
        IModuloService modulos, INotificacionService notificaciones)
    {
        _db = db;
        _archivos = archivos;
        _modulos = modulos;
        _notificaciones = notificaciones;
    }

    private string UsuarioId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // ---------- Escenario 1: apadrinar ----------

    [HttpGet("fundaciones/{slug}/apadrinar/{id:int}/confirmar")]
    public async Task<IActionResult> Confirmar(string slug, int id)
    {
        if (!Politicas.PuedeApoyar(User))
        {
            TempData["Error"] = Politicas.MensajeCuentaInstitucional;
            return Redirect($"/fundaciones/{slug}/apadrinar/{id}");
        }

        var (esal, b) = await BuscarApadrinableAsync(slug, id);
        if (esal is null) return NoDisponible("~/Views/Fundaciones/NoDisponible.cshtml", null);
        if (b is null) return NoDisponible("~/Views/Fundaciones/ApadrinableNoDisponible.cshtml", Contexto(esal));

        var existente = await ApadrinamientoActivoAsync(b.Id);
        if (existente is not null)
        {
            TempData["Mensaje"] = $"Ya eres padrino de {b.Nombre}. Aquí está tu apadrinamiento.";
            return RedirectToAction(nameof(Detalle), new { id = existente });
        }

        return View(await PrepararAsync(new ConfirmarApadrinamientoViewModel
        {
            ValorMensual = Formatear(b.AporteSugerido ?? ValorMinimo)
        }, esal, b));
    }

    [HttpPost("fundaciones/{slug}/apadrinar/{id:int}/confirmar")]
    public async Task<IActionResult> Confirmar(string slug, int id, ConfirmarApadrinamientoViewModel model)
    {
        if (!Politicas.PuedeApoyar(User))
        {
            TempData["Error"] = Politicas.MensajeCuentaInstitucional;
            return Redirect($"/fundaciones/{slug}/apadrinar/{id}");
        }

        var (esal, b) = await BuscarApadrinableAsync(slug, id);
        if (esal is null) return NoDisponible("~/Views/Fundaciones/NoDisponible.cshtml", null);
        if (b is null) return NoDisponible("~/Views/Fundaciones/ApadrinableNoDisponible.cshtml", Contexto(esal));

        var existente = await ApadrinamientoActivoAsync(b.Id);
        if (existente is not null)
            return RedirectToAction(nameof(Detalle), new { id = existente });

        var preparado = await PrepararAsync(model, esal, b);
        if (!preparado.Disponible) return View(preparado);

        var valor = Formatos.LeerPesos(model.ValorMensual);
        if (!string.IsNullOrWhiteSpace(model.ValorMensual))
        {
            if (valor is null)
                ModelState.AddModelError(nameof(model.ValorMensual), "Escribe el valor solo con números, por ejemplo 30.000.");
            else if (valor < ValorMinimo)
                ModelState.AddModelError(nameof(model.ValorMensual), $"El aporte mensual mínimo es {Formatos.Pesos(ValorMinimo)}.");
            else if (valor > ValorMaximo)
                ModelState.AddModelError(nameof(model.ValorMensual), $"Para aportes de más de {Formatos.Pesos(ValorMaximo)} comunícate directamente con la fundación.");
        }
        if (!ModelState.IsValid) return View(preparado);

        var apadrinamiento = new Apadrinamiento
        {
            EsalId = esal.Id,
            BeneficiarioId = b.Id,
            PadrinoId = UsuarioId,
            ValorMensual = valor!.Value
        };
        _db.Apadrinamientos.Add(apadrinamiento);
        await _db.SaveChangesAsync();

        await _notificaciones.AgregarAAdministradoresAsync(esal.Id,
            "Nuevo padrino",
            $"{User.FindFirstValue(MunerAppClaims.NombreCompleto) ?? "Un donante"} apadrinó a {b.Nombre} con un aporte mensual de {valor:C0}.",
            $"/Fundacion/Beneficiarios/Detalle/{b.Id}", "bi-balloon-heart");
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"¡Gracias! Ahora eres padrino de {b.Nombre}. Transfiere tu primer aporte a la cuenta oficial y repórtalo para que {esal.Nombre} lo confirme.";
        return RedirectToAction(nameof(Detalle), new { id = apadrinamiento.Id });
    }

    // ---------- Mis apadrinamientos ----------

    [HttpGet("mis-apadrinamientos")]
    public async Task<IActionResult> Index()
    {
        var lista = await _db.Apadrinamientos.IgnoreQueryFilters().AsNoTracking()
            .Where(a => a.PadrinoId == UsuarioId)
            .OrderBy(a => a.Estado).ThenByDescending(a => a.FechaInicio)
            .Select(a => new
            {
                a.Id, Beneficiario = a.Beneficiario!.Nombre, a.Beneficiario.Apadrinable, a.Beneficiario.FotoPublicaRuta,
                Esal = a.Esal!.Nombre, a.Esal.Slug, a.Esal.LogoRuta, a.ValorMensual, a.Estado, a.FechaInicio, a.FechaCancelacion
            })
            .ToListAsync();

        return View(lista.Select(a => new ApadrinamientoItem
        {
            Id = a.Id,
            NombreBeneficiario = a.Beneficiario,
            FotoUrl = a.Apadrinable && a.FotoPublicaRuta is not null ? _archivos.UrlPublica(a.FotoPublicaRuta) : null,
            NombreEsal = a.Esal,
            SlugEsal = a.Slug,
            LogoUrl = a.LogoRuta is null ? null : _archivos.UrlPublica(a.LogoRuta),
            ValorMensual = a.ValorMensual,
            Estado = a.Estado,
            FechaInicio = a.FechaInicio,
            FechaCancelacion = a.FechaCancelacion
        }).ToList());
    }

    [HttpGet("mis-apadrinamientos/{id:int}")]
    public async Task<IActionResult> Detalle(int id)
    {
        var a = await BuscarPropioAsync(id);
        if (a is null) return NotFound();

        var datos = await _db.DatosDonacion.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(d => d.EsalId == a.EsalId);
        var aportes = await _db.Donaciones.IgnoreQueryFilters().AsNoTracking()
            .Where(d => d.ApadrinamientoId == id && d.DonanteId == UsuarioId)
            .OrderByDescending(d => d.FechaTransferencia).ThenByDescending(d => d.Id)
            .Select(d => new AporteApadrinamiento(d.Codigo, d.Valor, d.FechaTransferencia, d.Estado))
            .ToListAsync();

        var b = a.Beneficiario!;
        return View(new ApadrinamientoDetalleViewModel
        {
            Id = a.Id,
            BeneficiarioId = b.Id,
            EstadoBeneficiario = b.Estado,
            NombreBeneficiario = b.Nombre,
            BeneficiarioVisible = b.Apadrinable,
            FotoUrl = b.Apadrinable && b.FotoPublicaRuta is not null ? _archivos.UrlPublica(b.FotoPublicaRuta) : null,
            NombreEsal = a.Esal!.Nombre,
            SlugEsal = a.Esal.Slug,
            LogoUrl = a.Esal.LogoRuta is null ? null : _archivos.UrlPublica(a.Esal.LogoRuta),
            ValorMensual = a.ValorMensual,
            Estado = a.Estado,
            FechaInicio = a.FechaInicio,
            FechaCancelacion = a.FechaCancelacion,
            DatosDisponibles = datos is not null,
            TipoCuenta = datos is null ? null : Textos.De(datos.TipoCuenta, datos.TipoLlave),
            Entidad = datos?.Entidad,
            Numero = datos?.Numero,
            Titular = datos?.Titular,
            DocumentoTitular = datos?.DocumentoTitular,
            Instrucciones = datos?.Instrucciones,
            Aportes = aportes
        });
    }

    // ---------- Escenario 3: cancelar ----------

    [HttpPost("mis-apadrinamientos/{id:int}/cancelar")]
    public async Task<IActionResult> Cancelar(int id)
    {
        var a = await _db.Apadrinamientos.IgnoreQueryFilters()
            .Include(x => x.Beneficiario)
            .FirstOrDefaultAsync(x => x.Id == id && x.PadrinoId == UsuarioId);
        if (a is null) return NotFound();
        if (a.Estado != EstadoApadrinamiento.Activo)
        {
            TempData["Error"] = "Este apadrinamiento ya estaba cancelado.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        a.Estado = EstadoApadrinamiento.Cancelado;
        a.FechaCancelacion = DateTime.UtcNow;
        await _notificaciones.AgregarAAdministradoresAsync(a.EsalId,
            "Un padrino canceló su apadrinamiento",
            $"{User.FindFirstValue(MunerAppClaims.NombreCompleto) ?? "Un padrino"} canceló su apadrinamiento de {a.Beneficiario!.Nombre} (aporte de {a.ValorMensual:C0} al mes).",
            $"/Fundacion/Beneficiarios/Detalle/{a.BeneficiarioId}", "bi-heartbreak");
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"Cancelaste tu apadrinamiento de {a.Beneficiario!.Nombre}. Gracias por haberlo acompañado.";
        return RedirectToAction(nameof(Detalle), new { id });
    }

    // ---------- Auxiliares ----------

    /// <summary>La fundación (activa y con el módulo de beneficiarios) y el beneficiario, solo si todavía se ofrece al público.</summary>
    private async Task<(Esal? Esal, BeneficiarioApadrinable? Beneficiario)> BuscarApadrinableAsync(string slug, int id)
    {
        var esal = await _db.Esales.AsNoTracking().FirstOrDefaultAsync(e => e.Slug == slug && e.Activa);
        if (esal is null || !await _modulos.EstaActivoAsync(esal.Id, CodigosModulo.Beneficiarios)) return (null, null);

        var b = await _db.Beneficiarios.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Id == id && x.EsalId == esal.Id && x.Apadrinable)
            .Select(x => new BeneficiarioApadrinable(x.Id, x.Nombre, x.FechaNacimiento, x.FotoPublicaRuta, x.AporteSugerido))
            .FirstOrDefaultAsync();
        return (esal, b);
    }

    private record BeneficiarioApadrinable(int Id, string Nombre, DateTime FechaNacimiento, string? FotoPublicaRuta, decimal? AporteSugerido);

    private Task<int?> ApadrinamientoActivoAsync(int beneficiarioId)
        => _db.Apadrinamientos.IgnoreQueryFilters().AsNoTracking()
            .Where(a => a.PadrinoId == UsuarioId && a.BeneficiarioId == beneficiarioId && a.Estado == EstadoApadrinamiento.Activo)
            .Select(a => (int?)a.Id)
            .FirstOrDefaultAsync();

    private Task<Apadrinamiento?> BuscarPropioAsync(int id)
        => _db.Apadrinamientos.IgnoreQueryFilters().AsNoTracking()
            .Include(a => a.Esal).Include(a => a.Beneficiario)
            .FirstOrDefaultAsync(a => a.Id == id && a.PadrinoId == UsuarioId);

    private async Task<ConfirmarApadrinamientoViewModel> PrepararAsync(ConfirmarApadrinamientoViewModel model, Esal esal, BeneficiarioApadrinable b)
    {
        model.Slug = esal.Slug!;
        model.NombreEsal = esal.Nombre;
        model.LogoUrl = esal.LogoRuta is null ? null : _archivos.UrlPublica(esal.LogoRuta);
        model.BeneficiarioId = b.Id;
        model.NombreBeneficiario = b.Nombre;
        model.Edad = Formatos.Edad(b.FechaNacimiento);
        model.FotoUrl = b.FotoPublicaRuta is null ? null : _archivos.UrlPublica(b.FotoPublicaRuta);
        model.AporteSugerido = b.AporteSugerido ?? ValorMinimo;
        model.Disponible = await _db.DatosDonacion.IgnoreQueryFilters().AnyAsync(d => d.EsalId == esal.Id);
        return model;
    }

    private ApadrinablesViewModel Contexto(Esal esal) => new()
    {
        Slug = esal.Slug!,
        Nombre = esal.Nombre,
        LogoUrl = esal.LogoRuta is null ? null : _archivos.UrlPublica(esal.LogoRuta)
    };

    private IActionResult NoDisponible(string vista, ApadrinablesViewModel? modelo)
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return modelo is null ? View(vista) : View(vista, modelo);
    }

    private static string Formatear(decimal valor) => ((long)valor).ToString("N0", new System.Globalization.CultureInfo("es-CO"));
}
