using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Models.Publico;
using MunerApp.Web.Servicios;

namespace MunerApp.Web.Controllers;

/// <summary>
/// HU-043: el donante paga en línea a una causa con el Web Checkout de Wompi (tarjeta, PSE, Nequi).
/// El dinero llega a la cuenta Wompi de la fundación.
/// Antes de ir al checkout se crea la donación en "Pendiente", con <see cref="OrigenDonacion.Wompi"/> y una
/// <see cref="Donacion.ReferenciaPasarela"/> única y firmada. El cambio a Confirmada o Rechazada, la suma a la
/// causa y el comprobante los hace el aviso de Wompi (HU-044, <see cref="ConfirmacionPagosWompi"/>):
/// la página de resultado solo consulta la transacción y la muestra, así nunca se suma dos veces.
/// Consulta con IgnoreQueryFilters porque el donante paga a fundaciones distintas: siempre filtra por el usuario autenticado.
/// </summary>
[Authorize]
public class PagosWompiController : Controller
{
    private const decimal ValorMinimo = 5_000m;
    private const decimal ValorMaximo = 50_000_000m;
    private const string Moneda = "COP";
    private const string UrlCheckout = "https://checkout.wompi.co/p/";

    private readonly MunerAppDbContext _db;
    private readonly CausasPublicas _causas;
    private readonly IWompiService _wompi;
    private readonly ISecretosService _secretos;
    private readonly IAlmacenamientoArchivos _archivos;
    private readonly ILogger<PagosWompiController> _logger;

    public PagosWompiController(MunerAppDbContext db, CausasPublicas causas, IWompiService wompi,
        ISecretosService secretos, IAlmacenamientoArchivos archivos, ILogger<PagosWompiController> logger)
    {
        _db = db;
        _causas = causas;
        _wompi = wompi;
        _secretos = secretos;
        _archivos = archivos;
        _logger = logger;
    }

    private string UsuarioId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // ---------- Paso 1: elegir el valor ----------

    [HttpGet("fundaciones/{slug}/causas/{causaId:int}/donar-en-linea")]
    public async Task<IActionResult> Donar(string slug, int causaId)
    {
        var (causa, error) = await ValidarCausaAsync(slug, causaId);
        if (causa is null) return NotFound();
        if (error is not null)
        {
            TempData["Error"] = error;
            return Redirect(causa.UrlRegreso);
        }
        return View(Modelo(causa.Detalle, string.Empty));
    }

    // ---------- Paso 2: crear la donación pendiente y enviar al checkout ----------

    [HttpPost("fundaciones/{slug}/causas/{causaId:int}/donar-en-linea")]
    public async Task<IActionResult> Donar(string slug, int causaId, DonarEnLineaViewModel model)
    {
        var (causa, error) = await ValidarCausaAsync(slug, causaId);
        if (causa is null) return NotFound();
        if (error is not null)
        {
            TempData["Error"] = error;
            return Redirect(causa.UrlRegreso);
        }

        var valor = LeerValor(model.Valor);
        if (!string.IsNullOrWhiteSpace(model.Valor) && valor is null)
            ModelState.AddModelError(nameof(model.Valor), "Escribe el valor solo con números, por ejemplo 50.000.");
        else if (valor is < ValorMinimo)
            ModelState.AddModelError(nameof(model.Valor), $"El valor mínimo para pagar en línea es {Formatos.Pesos(ValorMinimo)}.");
        else if (valor is > ValorMaximo)
            ModelState.AddModelError(nameof(model.Valor), $"Para donaciones de más de {Formatos.Pesos(ValorMaximo)} comunícate directamente con la fundación.");
        if (!ModelState.IsValid) return View(Modelo(causa.Detalle, model.Valor));

        // Wompi bloquea (403) el checkout si la dirección de regreso es local
        if (Request.Host.Host is "localhost" or "127.0.0.1" or "::1" or "[::1]")
        {
            ModelState.AddModelError(string.Empty, "Wompi no acepta volver a una dirección local (localhost). Abre la página desde un túnel público (Dev Tunnels de Visual Studio o ngrok) para probar el pago.");
            return View(Modelo(causa.Detalle, model.Valor));
        }

        var config = await _db.ConfigPasarelas.IgnoreQueryFilters().AsNoTracking()
            .FirstAsync(p => p.EsalId == causa.EsalId && p.Activa);

        string secretoIntegridad;
        try
        {
            secretoIntegridad = _secretos.Desproteger(config.SecretoIntegridadCifrado);
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            _logger.LogError(ex, "No se pudo descifrar el secreto de integridad de Wompi de la ESAL {EsalId}.", causa.EsalId);
            TempData["Error"] = "El pago en línea de esta fundación no está disponible en este momento. Puedes donar por transferencia.";
            return Redirect(causa.UrlRegreso);
        }

        // La donación nace pendiente; el aviso de Wompi (HU-044) la confirma o la rechaza
        var ahora = DateTime.UtcNow;
        var donacion = new Donacion
        {
            EsalId = causa.EsalId,
            Codigo = "TMP-" + Guid.NewGuid().ToString("N")[..16],
            DonanteId = UsuarioId,
            Valor = valor!.Value,
            FechaTransferencia = Formatos.Local(ahora).Date,
            MedioPago = "Wompi",
            SoporteRuta = string.Empty,
            Estado = EstadoDonacion.Pendiente,
            FechaReporte = ahora,
            CausaId = causaId,
            Origen = OrigenDonacion.Wompi,
            ReferenciaPasarela = $"MUN-{causa.EsalId}-{Guid.NewGuid():N}"
        };
        _db.Donaciones.Add(donacion);
        await _db.SaveChangesAsync();
        donacion.Codigo = $"DON-{ahora:yyyy}-{donacion.Id:D6}";
        await _db.SaveChangesAsync();

        // El monto va en centavos; la firma es SHA256(referencia + centavos + moneda + secreto de integridad)
        var centavos = (long)(donacion.Valor * 100);
        var firma = _wompi.GenerarFirmaIntegridad(donacion.ReferenciaPasarela, centavos, Moneda, secretoIntegridad);

        // Wompi vuelve a esta dirección y le agrega ?id=<transacción>
        var retorno = $"{Request.Scheme}://{Request.Host}/pagos/resultado/{donacion.ReferenciaPasarela}";
        var url = UrlCheckout
            + $"?public-key={Uri.EscapeDataString(config.LlavePublica)}"
            + $"&currency={Moneda}"
            + $"&amount-in-cents={centavos}"
            + $"&reference={Uri.EscapeDataString(donacion.ReferenciaPasarela)}"
            + $"&signature:integrity={firma}"
            + $"&redirect-url={Uri.EscapeDataString(retorno)}";
        return Redirect(url);
    }

    // ---------- Paso 3: resultado (solo muestra; no cambia la donación) ----------

    [HttpGet("pagos/resultado/{referencia}")]
    public async Task<IActionResult> Resultado(string referencia, string? id)
    {
        var donacion = await _db.Donaciones.IgnoreQueryFilters().AsNoTracking()
            .Include(d => d.Esal).Include(d => d.Causa)
            .FirstOrDefaultAsync(d => d.ReferenciaPasarela == referencia && d.Origen == OrigenDonacion.Wompi && d.DonanteId == UsuarioId);
        if (donacion is null || donacion.CausaId is null) return NotFound();

        var modelo = new ResultadoPagoViewModel
        {
            CodigoDonacion = donacion.Codigo,
            Valor = donacion.Valor,
            Referencia = referencia,
            MetodoPago = donacion.MedioPago,
            NombreEsal = donacion.Esal!.Nombre,
            Slug = donacion.Esal.Slug ?? string.Empty,
            LogoUrl = donacion.Esal.LogoRuta is null ? null : _archivos.UrlPublica(donacion.Esal.LogoRuta),
            CausaId = donacion.CausaId.Value,
            TituloCausa = donacion.Causa?.Titulo ?? string.Empty
        };

        // Si el aviso de Wompi ya llegó, eso es lo oficial
        if (donacion.Estado == EstadoDonacion.Confirmada)
        {
            modelo.Resultado = ResultadoPagoEnLinea.Confirmada;
            return View(modelo);
        }
        if (donacion.Estado == EstadoDonacion.Rechazada)
        {
            modelo.Resultado = ResultadoPagoEnLinea.NoAprobado;
            return View(modelo);
        }

        // Todavía pendiente: se consulta la transacción en Wompi, sin confiar en lo que dice la URL
        var transaccionId = !string.IsNullOrWhiteSpace(id) ? id.Trim() : donacion.TransaccionPasarelaId;
        var config = await _db.ConfigPasarelas.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(p => p.EsalId == donacion.EsalId);
        var tx = transaccionId is null || config is null ? null
            : await _wompi.ConsultarTransaccionAsync(transaccionId, config.Ambiente);

        if (tx is null || tx.Referencia != referencia || tx.MontoCentavos != (long)(donacion.Valor * 100) || tx.Moneda != Moneda)
        {
            modelo.Resultado = ResultadoPagoEnLinea.SinVerificar;
            return View(modelo);
        }

        if (tx.MetodoPago is not null) modelo.MetodoPago = $"Wompi · {tx.MetodoPago}";
        modelo.Resultado = tx.Estado switch
        {
            EstadoTransaccionWompi.Aprobada => ResultadoPagoEnLinea.AprobadoPorConfirmar,
            EstadoTransaccionWompi.Pendiente => ResultadoPagoEnLinea.EnProceso,
            _ => ResultadoPagoEnLinea.NoAprobado
        };
        return View(modelo);
    }

    // ---------- Apoyo ----------

    private record CausaValidada(int EsalId, CausaDetalleViewModel Detalle)
    {
        public string UrlRegreso => $"/fundaciones/{Detalle.SlugEsal}/causas/{Detalle.Id}";
    }

    /// <summary>La causa debe existir, recibir donaciones y su fundación debe tener Wompi activo.</summary>
    private async Task<(CausaValidada? Causa, string? Error)> ValidarCausaAsync(string slug, int causaId)
    {
        var esalId = await _db.Esales.AsNoTracking()
            .Where(e => e.Slug == slug && e.Activa).Select(e => (int?)e.Id).FirstOrDefaultAsync();
        var detalle = esalId is int eid ? await _causas.ObtenerAsync(eid, causaId) : null;
        if (detalle is null) return (null, null);

        var causa = new CausaValidada(esalId!.Value, detalle);
        if (!detalle.RecibeDonaciones)
            return (causa, "Esta causa ya no recibe donaciones.");
        if (!detalle.PagosEnLineaActivos)
            return (causa, "Esta fundación todavía no tiene activo el pago en línea. Puedes donar por transferencia.");
        return (causa, null);
    }

    private static DonarEnLineaViewModel Modelo(CausaDetalleViewModel c, string valor) => new()
    {
        CausaId = c.Id, TituloCausa = c.Titulo, NombreEsal = c.NombreEsal, Slug = c.SlugEsal, LogoUrl = c.LogoUrl,
        Meta = c.Meta, Recaudado = c.Recaudado, Porcentaje = c.Porcentaje, Valor = valor
    };

    /// <summary>Acepta "50000", "50.000", "$ 50.000" o "50,000" (pesos sin decimales).</summary>
    private static decimal? LeerValor(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var limpio = texto.Replace("$", "").Replace("COP", "", StringComparison.OrdinalIgnoreCase).Trim();
        if (limpio.Any(c => !char.IsDigit(c) && c is not '.' and not ',' and not ' ')) return null;
        var digitos = new string(limpio.Where(char.IsDigit).ToArray());
        return digitos.Length is > 0 and <= 12 ? decimal.Parse(digitos) : null;
    }
}
