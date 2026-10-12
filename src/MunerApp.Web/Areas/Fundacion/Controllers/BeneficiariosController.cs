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
using MunerApp.Web.Areas.Fundacion.Models;
using MunerApp.Web.Filtros;
using MunerApp.Web.Seguridad;
using MunerApp.Web.Servicios;
using MunerApp.Web.Validacion;

namespace MunerApp.Web.Areas.Fundacion.Controllers;

/// <summary>
/// HU-017: hoja de vida de los beneficiarios (en el piloto, los gatos). Información interna de la fundación.
/// HU-018: administradores y voluntarios consultan el listado y la hoja de vida.
/// Solo el administrador principal registra, edita, cambia estados y registra al adoptante;
/// los administradores (principal y de consulta) ven los datos del adoptante y los padrinos.
/// Todo requiere que la fundación tenga activo el módulo de beneficiarios.
/// El filtro global por ESAL garantiza que nunca se vea un beneficiario de otra fundación.
/// </summary>
[Area("Fundacion")]
[Authorize(Roles = Roles.AdministradorESAL + "," + Roles.Voluntario)]
[RequiereModulo(CodigosModulo.Beneficiarios)]
public class BeneficiariosController : Controller
{
    private readonly MunerAppDbContext _db;
    private readonly IEsalActual _esalActual;
    private readonly IAlmacenamientoArchivos _archivos;
    private readonly INotificacionService _notificaciones;

    public BeneficiariosController(MunerAppDbContext db, IEsalActual esalActual, IAlmacenamientoArchivos archivos,
        INotificacionService notificaciones)
    {
        _db = db;
        _esalActual = esalActual;
        _archivos = archivos;
        _notificaciones = notificaciones;
    }

    private const int PorPagina = 12;

    private string UsuarioId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private int EsalId => _esalActual.EsalId ?? throw new InvalidOperationException("El usuario no pertenece a una ESAL.");

    // ---------- HU-018: listado interno con filtros ----------

    [HttpGet]
    public async Task<IActionResult> Index(EstadoBeneficiario? estado, string? q, PendienteBeneficiario? pendiente, int pagina = 1)
    {
        var esPrincipal = Politicas.EsAdminPrincipal(User);

        var conteos = await _db.Beneficiarios.AsNoTracking()
            .GroupBy(b => b.Estado)
            .Select(g => new { g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Cantidad);

        var consulta = _db.Beneficiarios.AsNoTracking().AsQueryable();
        if (estado is not null && Enum.IsDefined(estado.Value))
            consulta = consulta.Where(b => b.Estado == estado.Value);
        else
            estado = null;

        // Desde el tablero: gatos que siguen en la fundación y tienen algo pendiente
        if (pendiente is not null && Enum.IsDefined(pendiente.Value))
        {
            var enCasa = ReglasBeneficiario.EnCasa;
            var hoy = Formatos.Hoy();
            consulta = consulta.Where(b => enCasa.Contains(b.Estado));
            if (pendiente == PendienteBeneficiario.Examen)
                consulta = consulta.Where(b => b.FechaExamenIngreso == null);
            else
            {
                var tipo = pendiente == PendienteBeneficiario.Vacuna ? TipoEventoClinico.Vacuna : TipoEventoClinico.Desparasitacion;
                var desde = hoy.AddDays(pendiente == PendienteBeneficiario.Vacuna ? -ReglasBeneficiario.DiasVacuna : -ReglasBeneficiario.DiasDesparasitacion);
                consulta = consulta.Where(b => !_db.EventosClinicos.Any(e => e.BeneficiarioId == b.Id && e.Tipo == tipo && e.Fecha >= desde));
            }
        }
        else
            pendiente = null;

        q = q?.Trim();
        if (!string.IsNullOrEmpty(q))
            consulta = consulta.Where(b => b.Nombre.Contains(q));

        var total = await consulta.CountAsync();
        var totalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)PorPagina));
        pagina = Math.Clamp(pagina, 1, totalPaginas);

        var items = await consulta
            .OrderBy(b => b.Nombre)
            .Skip((pagina - 1) * PorPagina)
            .Take(PorPagina)
            .Select(b => new BeneficiarioItem
            {
                Id = b.Id,
                Nombre = b.Nombre,
                FechaNacimiento = b.FechaNacimiento,
                Sexo = b.Sexo,
                Color = b.Color,
                Estado = b.Estado,
                TieneFoto = b.FotoRuta != null,
                Apadrinable = b.Apadrinable,
                FaltaAdoptante = esPrincipal && b.Estado == EstadoBeneficiario.Adoptado && b.Adoptante == null
            }).ToListAsync();

        return View(new BeneficiariosIndexViewModel
        {
            Beneficiarios = items,
            Estado = estado,
            Pendiente = pendiente,
            Busqueda = q,
            Conteos = conteos,
            Total = total,
            Pagina = pagina,
            TotalPaginas = totalPaginas,
            PuedeGestionar = esPrincipal
        });
    }

    // ---------- Escenario 1: registro exitoso ----------

    [HttpGet]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public IActionResult Crear() => View(new BeneficiarioFormViewModel { FechaRescate = DateTime.Today });

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    [RequestSizeLimit(ValidadorArchivos.LimitePeticionBytes)]
    public async Task<IActionResult> Crear(BeneficiarioFormViewModel model)
    {
        var foto = await ValidarAsync(model);
        if (!ModelState.IsValid) return View(model);

        string? clave = null;
        if (foto is not null)
        {
            await using var stream = model.Foto!.OpenReadStream();
            clave = await _archivos.GuardarAsync(stream, $"esal/{EsalId}/beneficiarios", foto.Extension, publico: false);
        }

        var b = new Beneficiario
        {
            EsalId = EsalId,
            Nombre = model.Nombre.Trim(),
            FechaNacimiento = Nacimiento(model),
            FechaNacimientoExacta = model.FechaNacimiento is not null,
            Sexo = model.Sexo!.Value,
            Color = model.Color.Trim(),
            FechaRescate = model.FechaRescate!.Value.Date,
            Estado = EstadoBeneficiario.EnLaFundacion, // estado inicial fijo
            FotoRuta = clave,
            RegistradoPorId = UsuarioId
        };
        AplicarResena(b, model);
        b.HistorialEstados.Add(new HistorialEstadoBeneficiario
        {
            EsalId = b.EsalId,
            Estado = EstadoBeneficiario.EnLaFundacion,
            CambiadoPorId = UsuarioId,
            Nota = "Registro de la hoja de vida"
        });
        _db.Beneficiarios.Add(b);
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"Registraste a {b.Nombre}. Su estado inicial es \"En la fundación\".";
        return RedirectToAction(nameof(Detalle), new { id = b.Id });
    }

    // ---------- Hoja de vida ----------

    [HttpGet]
    public async Task<IActionResult> Detalle(int id)
    {
        var esAdmin = User.IsInRole(Roles.AdministradorESAL);
        var esPrincipal = Politicas.EsAdminPrincipal(User);
        var verClinica = Politicas.TieneAccesoClinico(User);
        var b = await _db.Beneficiarios.AsNoTracking()
            .Include(x => x.Adoptante)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();

        var historial = await (from h in _db.HistorialEstadosBeneficiario.AsNoTracking()
                               where h.BeneficiarioId == id
                               join u in _db.Users on h.CambiadoPorId equals u.Id into us
                               from u in us.DefaultIfEmpty()
                               orderby h.Fecha descending, h.Id descending
                               select new CambioEstadoItem(h.Estado, h.Fecha, h.Nota, u == null ? null : u.NombreCompleto))
            .ToListAsync();

        var registrador = b.RegistradoPorId is null ? null
            : await _db.Users.AsNoTracking().Where(u => u.Id == b.RegistradoPorId).Select(u => u.NombreCompleto).FirstOrDefaultAsync();

        var padrinos = !esAdmin ? new List<PadrinoItem>() : await (
            from a in _db.Apadrinamientos.AsNoTracking()
            where a.BeneficiarioId == id
            join u in _db.Users on a.PadrinoId equals u.Id
            orderby a.Estado, a.FechaInicio descending
            select new PadrinoItem(
                u.NombreCompleto, u.Email ?? "", a.ValorMensual, a.FechaInicio, a.Estado == EstadoApadrinamiento.Activo,
                _db.Donaciones.Where(d => d.ApadrinamientoId == a.Id && d.Estado == EstadoDonacion.Confirmada).Sum(d => (decimal?)d.Valor) ?? 0))
            .ToListAsync();

        return View(new BeneficiarioDetalleViewModel
        {
            Padrinos = padrinos,
            Id = b.Id,
            Nombre = b.Nombre,
            FechaNacimiento = b.FechaNacimiento,
            Sexo = b.Sexo,
            Color = b.Color,
            Estado = b.Estado,
            TieneFoto = b.FotoRuta is not null,
            FechaRescate = b.FechaRescate,
            FechaNacimientoExacta = b.FechaNacimientoExacta,
            Raza = b.Raza,
            PesoIngresoKg = b.PesoIngresoKg,
            Procedencia = b.Procedencia,
            DetallesProcedencia = b.DetallesProcedencia,
            EstadoReproductivo = b.EstadoReproductivo,
            SenalesParticulares = b.SenalesParticulares,
            PuedeRegistrarClinica = Politicas.PuedeRegistrarClinica(User),
            ExamenIngreso = verClinica ? await ExamenDeAsync(b) : null,
            Etogramas = await EtogramasAsync(b.Id),
            FechaRegistro = b.FechaRegistro,
            RegistradoPor = registrador,
            PuedeGestionar = esPrincipal,
            EsAdministrador = esAdmin,
            PuedePublicar = esPrincipal,
            Apadrinable = b.Apadrinable,
            HistoriaPublica = b.HistoriaPublica,
            AporteSugerido = b.AporteSugerido,
            TieneFotoPublica = b.FotoPublicaRuta is not null,
            SlugEsal = await _db.Esales.AsNoTracking().Where(e => e.Id == b.EsalId).Select(e => e.Slug).FirstOrDefaultAsync(),
            PuedeVerClinica = verClinica,
            EventosClinicos = verClinica ? await _db.EventosClinicos.CountAsync(e => e.BeneficiarioId == id) : 0,
            FaltaAdoptante = esPrincipal && b.Estado == EstadoBeneficiario.Adoptado && b.Adoptante is null,
            Historial = historial,
            // Los datos personales del adoptante son solo para los administradores
            Adoptante = !esAdmin || b.Adoptante is null ? null : new AdoptanteItem
            {
                Nombre = b.Adoptante.Nombre,
                Documento = b.Adoptante.Documento,
                NumeroFormulario = b.Adoptante.NumeroFormulario,
                Elaboro = b.Adoptante.Elaboro,
                Telefono = b.Adoptante.Telefono,
                Correo = b.Adoptante.Correo,
                Ciudad = b.Adoptante.Ciudad,
                Direccion = b.Adoptante.Direccion,
                FechaAdopcion = b.Adoptante.FechaAdopcion,
                Observaciones = b.Adoptante.Observaciones
            }
        });
    }

    /// <summary>La foto es interna: se entrega solo a usuarios de la fundación dueña del beneficiario.</summary>
    [HttpGet]
    public async Task<IActionResult> Foto(int id)
    {
        var ruta = await _db.Beneficiarios.AsNoTracking().Where(b => b.Id == id).Select(b => b.FotoRuta).FirstOrDefaultAsync();
        if (ruta is null) return NotFound();
        var stream = await _archivos.AbrirAsync(ruta);
        if (stream is null) return NotFound();
        Response.Headers.CacheControl = "private,max-age=300";
        return File(stream, ValidadorArchivos.ContentTypeDe(ruta));
    }

    // ---------- Examen semiológico de ingreso (clínico) ----------

    [HttpGet]
    [Authorize(Policy = Politicas.RegistroClinico)]
    public async Task<IActionResult> ExamenIngreso(int id)
    {
        var b = await _db.Beneficiarios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();
        var modelo = await ExamenDeAsync(b);
        return View(modelo);
    }

    [HttpPost]
    [Authorize(Policy = Politicas.RegistroClinico)]
    public async Task<IActionResult> ExamenIngreso(int id, ExamenIngresoViewModel model)
    {
        var b = await _db.Beneficiarios.FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();
        model.BeneficiarioId = b.Id;
        model.NombreBeneficiario = b.Nombre;

        decimal? Leer(string? texto, string campo, decimal min, decimal max, string ejemplo)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;
            var v = Formatos.LeerDecimal(texto);
            if (v is null || v < min || v > max)
                ModelState.AddModelError(campo, $"Escribe un número válido, por ejemplo {ejemplo}.");
            return v;
        }
        var tllc = Leer(model.Tllc, nameof(model.Tllc), 0, 30, "2");
        var rpc = Leer(model.Rpc, nameof(model.Rpc), 0, 30, "2");
        var temperatura = Leer(model.Temperatura, nameof(model.Temperatura), 30, 45, "38,6");
        if (!ModelState.IsValid) return View(model);

        b.FrecuenciaRespiratoria = model.FrecuenciaRespiratoria;
        b.FrecuenciaCardiaca = model.FrecuenciaCardiaca;
        b.Tllc = tllc;
        b.Rpc = rpc;
        b.Temperatura = temperatura;
        b.CondicionCorporal = model.CondicionCorporal;
        b.MucosaConjuntival = Limpiar(model.MucosaConjuntival);
        b.MucosaOral = Limpiar(model.MucosaOral);
        b.MucosaRectal = Limpiar(model.MucosaRectal);
        b.MucosaVulvarPrepucial = Limpiar(model.MucosaVulvarPrepucial);
        b.EstadoConciencia = model.EstadoConciencia;
        b.ObservacionesIngreso = Limpiar(model.Observaciones);
        b.FechaExamenIngreso = DateTime.UtcNow;
        b.ExamenIngresoPorId = UsuarioId;
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"Guardaste el examen de ingreso de {b.Nombre}.";
        return RedirectToAction(nameof(Detalle), new { id });
    }

    private async Task<ExamenIngresoViewModel> ExamenDeAsync(Beneficiario b) => new()
    {
        BeneficiarioId = b.Id,
        NombreBeneficiario = b.Nombre,
        FechaExamen = b.FechaExamenIngreso,
        RealizadoPor = b.ExamenIngresoPorId is null ? null
            : await _db.Users.AsNoTracking().Where(u => u.Id == b.ExamenIngresoPorId).Select(u => u.NombreCompleto).FirstOrDefaultAsync(),
        FrecuenciaRespiratoria = b.FrecuenciaRespiratoria,
        FrecuenciaCardiaca = b.FrecuenciaCardiaca,
        Tllc = b.Tllc is decimal t ? Formatos.Numero(t) : null,
        Rpc = b.Rpc is decimal r ? Formatos.Numero(r) : null,
        Temperatura = b.Temperatura is decimal tmp ? Formatos.Numero(tmp) : null,
        CondicionCorporal = b.CondicionCorporal,
        MucosaConjuntival = b.MucosaConjuntival,
        MucosaOral = b.MucosaOral,
        MucosaRectal = b.MucosaRectal,
        MucosaVulvarPrepucial = b.MucosaVulvarPrepucial,
        EstadoConciencia = b.EstadoConciencia,
        Observaciones = b.ObservacionesIngreso
    };

    // ---------- Etograma (comportamiento antes y después de la castración) ----------

    [HttpGet]
    [Authorize(Policy = Politicas.RegistroClinico)]
    public async Task<IActionResult> Etograma(int id)
    {
        var b = await _db.Beneficiarios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();
        var yaPre = await _db.EvaluacionesComportamiento.AnyAsync(e => e.BeneficiarioId == id && e.Momento == MomentoEtograma.PreCastracion);
        return View(new EtogramaFormViewModel
        {
            BeneficiarioId = b.Id,
            NombreBeneficiario = b.Nombre,
            Fecha = DateTime.Today,
            Momento = yaPre || b.EstadoReproductivo == EstadoReproductivo.Castrado ? MomentoEtograma.PostCastracion : MomentoEtograma.PreCastracion
        });
    }

    [HttpPost]
    [Authorize(Policy = Politicas.RegistroClinico)]
    public async Task<IActionResult> Etograma(int id, EtogramaFormViewModel model)
    {
        var b = await _db.Beneficiarios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();
        model.BeneficiarioId = b.Id;
        model.NombreBeneficiario = b.Nombre;

        var validas = Domain.Constantes.Etograma.Todas.Select(c => c.Codigo).ToHashSet();
        model.Conductas = model.Conductas.Where(validas.Contains).Distinct().ToList();
        if (model.Conductas.Count == 0)
            ModelState.AddModelError(nameof(model.Conductas), "Marca al menos una conducta observada.");
        if (model.Fecha is DateTime f && f.Date > DateTime.Today)
            ModelState.AddModelError(nameof(model.Fecha), "La fecha no puede ser futura.");
        if (!ModelState.IsValid) return View(model);

        _db.EvaluacionesComportamiento.Add(new EvaluacionComportamiento
        {
            EsalId = b.EsalId,
            BeneficiarioId = b.Id,
            Momento = model.Momento!.Value,
            Fecha = model.Fecha!.Value.Date,
            Conductas = string.Join(',', model.Conductas),
            Observaciones = Limpiar(model.Observaciones),
            RegistradoPorId = UsuarioId
        });
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"Guardaste el etograma de {b.Nombre} ({Textos.De(model.Momento!.Value).ToLowerInvariant()}).";
        return RedirectToAction(nameof(Detalle), null, new { id }, "etograma");
    }

    private async Task<List<EtogramaItem>> EtogramasAsync(int beneficiarioId)
    {
        var lista = await (from e in _db.EvaluacionesComportamiento.AsNoTracking()
                           where e.BeneficiarioId == beneficiarioId
                           join u in _db.Users on e.RegistradoPorId equals u.Id into us
                           from u in us.DefaultIfEmpty()
                           orderby e.Fecha, e.Id
                           select new { e.Id, e.Momento, e.Fecha, e.Conductas, e.Observaciones, e.Metodo, Nombre = u == null ? null : u.NombreCompleto })
            .ToListAsync();
        return lista.Select(e => new EtogramaItem(e.Id, e.Momento, e.Fecha,
            e.Conductas.Split(',', StringSplitOptions.RemoveEmptyEntries), e.Observaciones, e.Metodo, e.Nombre)).ToList();
    }

    // ---------- Edición de los datos ----------

    [HttpGet]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> Editar(int id)
    {
        var b = await _db.Beneficiarios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();

        var (anios, meses) = Formatos.AniosYMeses(b.FechaNacimiento);
        return View(new BeneficiarioFormViewModel
        {
            Id = b.Id,
            Nombre = b.Nombre,
            EdadAnios = anios,
            EdadMeses = meses,
            Sexo = b.Sexo,
            Color = b.Color,
            FechaRescate = b.FechaRescate,
            FechaNacimiento = b.FechaNacimientoExacta ? b.FechaNacimiento : null,
            Raza = b.Raza,
            PesoIngreso = b.PesoIngresoKg is decimal peso ? Formatos.Numero(peso) : null,
            Procedencia = b.Procedencia,
            DetallesProcedencia = b.DetallesProcedencia,
            EstadoReproductivo = b.EstadoReproductivo,
            SenalesParticulares = b.SenalesParticulares,
            TieneFoto = b.FotoRuta is not null
        });
    }

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    [RequestSizeLimit(ValidadorArchivos.LimitePeticionBytes)]
    public async Task<IActionResult> Editar(int id, BeneficiarioFormViewModel model)
    {
        var b = await _db.Beneficiarios.FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();

        model.Id = id;
        model.TieneFoto = b.FotoRuta is not null;
        var foto = await ValidarAsync(model);
        if (!ModelState.IsValid) return View(model);

        // Con fecha exacta se usa esa; si no, la fecha solo se recalcula si cambiaron los años o los meses
        var (aniosActuales, mesesActuales) = Formatos.AniosYMeses(b.FechaNacimiento);
        if (model.FechaNacimiento is not null)
        {
            b.FechaNacimiento = model.FechaNacimiento.Value.Date;
            b.FechaNacimientoExacta = true;
        }
        else if (b.FechaNacimientoExacta || model.EdadAnios != aniosActuales || model.EdadMeses != mesesActuales)
        {
            b.FechaNacimiento = Nacimiento(model);
            b.FechaNacimientoExacta = false;
        }
        AplicarResena(b, model);

        b.Nombre = model.Nombre.Trim();
        b.Sexo = model.Sexo!.Value;
        b.Color = model.Color.Trim();
        b.FechaRescate = model.FechaRescate!.Value.Date;

        string? fotoAnterior = null;
        if (foto is not null)
        {
            fotoAnterior = b.FotoRuta;
            await using var stream = model.Foto!.OpenReadStream();
            b.FotoRuta = await _archivos.GuardarAsync(stream, $"esal/{b.EsalId}/beneficiarios", foto.Extension, publico: false);
        }
        else if (model.QuitarFoto && b.FotoRuta is not null)
        {
            fotoAnterior = b.FotoRuta;
            b.FotoRuta = null;
        }

        await _db.SaveChangesAsync();
        if (fotoAnterior is not null) await _archivos.EliminarAsync(fotoAnterior);

        TempData["Mensaje"] = $"Guardaste los cambios de {b.Nombre}.";
        return RedirectToAction(nameof(Detalle), new { id });
    }

    // ---------- Escenario 2: cambio de estado ----------

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> CambiarEstado(int id, CambioEstadoViewModel model)
    {
        var b = await _db.Beneficiarios.FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();

        if (!ModelState.IsValid || model.Estado is null || !Enum.IsDefined(model.Estado.Value))
        {
            TempData["Error"] = "Selecciona el nuevo estado.";
            return RedirectToAction(nameof(Detalle), new { id });
        }
        if (model.Estado == b.Estado)
        {
            TempData["Error"] = $"{b.Nombre} ya está en estado \"{Textos.De(b.Estado)}\".";
            return RedirectToAction(nameof(Detalle), new { id });
        }
        if (!ReglasBeneficiario.PuedeCambiar(b.Estado, model.Estado.Value))
        {
            TempData["Error"] = b.Estado == EstadoBeneficiario.Fallecido
                ? $"{b.Nombre} está registrado como fallecido: su estado ya no se puede cambiar."
                : $"{b.Nombre} no puede pasar de \"{Textos.De(b.Estado)}\" a \"{Textos.De(model.Estado.Value)}\".";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        var nuevo = model.Estado.Value;
        b.Estado = nuevo;

        // Adoptado o fallecido: terminan sus apadrinamientos activos y se avisa a cada padrino
        var finalizados = 0;
        if (ReglasBeneficiario.TerminaApadrinamientos(nuevo))
        {
            var activos = await _db.Apadrinamientos
                .Where(a => a.BeneficiarioId == b.Id && a.Estado == EstadoApadrinamiento.Activo)
                .ToListAsync();
            foreach (var a in activos)
            {
                a.Estado = EstadoApadrinamiento.Cancelado;
                a.FechaCancelacion = DateTime.UtcNow;
                var (titulo, motivo, icono) = nuevo switch
                {
                    EstadoBeneficiario.Adoptado => ($"¡{b.Nombre} fue adoptado!", $"{b.Nombre} fue adoptado", "bi-house-heart"),
                    EstadoBeneficiario.EncontroSuHogar => ($"¡{b.Nombre} encontró su hogar!", $"{b.Nombre} encontró su hogar", "bi-house-heart"),
                    EstadoBeneficiario.Liberado => ($"{b.Nombre} fue liberado", $"{b.Nombre} fue liberado", "bi-tree"),
                    _ => ($"Lamentamos contarte que {b.Nombre} falleció", $"{b.Nombre} falleció", "bi-heart")
                };
                _notificaciones.Agregar(a.PadrinoId, titulo,
                    $"{motivo}, así que tu apadrinamiento terminó y ya no tienes que hacer más aportes. Gracias por haberlo acompañado." +
                    (nuevo == EstadoBeneficiario.Fallecido ? "" : " Puedes apadrinar a otro beneficiario de la fundación."),
                    $"/mis-apadrinamientos/{a.Id}", icono);
            }
            finalizados = activos.Count;
        }

        // Un beneficiario adoptado o fallecido ya no se ofrece para apadrinar (HU-020)
        var retiradoDeApadrinamiento = b.Apadrinable && !PuedeApadrinarse(nuevo);
        if (retiradoDeApadrinamiento)
            await RetirarDeApadrinamientoAsync(b);
        _db.HistorialEstadosBeneficiario.Add(new HistorialEstadoBeneficiario
        {
            EsalId = b.EsalId,
            BeneficiarioId = b.Id,
            Estado = nuevo,
            CambiadoPorId = UsuarioId,
            Nota = string.IsNullOrWhiteSpace(model.Nota) ? null : model.Nota.Trim()
        });
        await _db.SaveChangesAsync();

        if (nuevo == EstadoBeneficiario.Adoptado && !await _db.AdoptantesBeneficiario.AnyAsync(a => a.BeneficiarioId == id))
        {
            TempData["Mensaje"] = $"{b.Nombre} ahora está \"Adoptado\". Registra los datos del adoptante para el seguimiento." +
                (finalizados > 0 ? $" Terminaron sus apadrinamientos y avisamos a {(finalizados == 1 ? "su padrino" : "sus padrinos")}." : "");
            return RedirectToAction(nameof(Adoptante), new { id });
        }

        TempData["Mensaje"] = $"El estado de {b.Nombre} cambió a \"{Textos.De(nuevo)}\".{(retiradoDeApadrinamiento ? " También dejó de ofrecerse para apadrinar." : "")}" +
            (finalizados > 0 ? $" Terminaron {finalizados} {(finalizados == 1 ? "apadrinamiento" : "apadrinamientos")} y avisamos a {(finalizados == 1 ? "su padrino" : "sus padrinos")}." : "");
        return RedirectToAction(nameof(Detalle), new { id });
    }

    // ---------- Escenario 3: datos del adoptante ----------

    [HttpGet]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> Adoptante(int id)
    {
        var b = await _db.Beneficiarios.AsNoTracking().Include(x => x.Adoptante).FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();
        if (b.Estado != EstadoBeneficiario.Adoptado)
        {
            TempData["Error"] = "Los datos del adoptante solo se registran cuando el beneficiario está en estado \"Adoptado\".";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        var a = b.Adoptante;
        return View(new AdoptanteFormViewModel
        {
            BeneficiarioId = b.Id,
            NombreBeneficiario = b.Nombre,
            FechaRescate = b.FechaRescate,
            YaRegistrado = a is not null,
            Nombre = a?.Nombre ?? "",
            Documento = a?.Documento,
            Telefono = a?.Telefono ?? "",
            Correo = a?.Correo,
            Ciudad = a?.Ciudad ?? "",
            Direccion = a?.Direccion,
            NumeroFormulario = a?.NumeroFormulario,
            Elaboro = a?.Elaboro ?? User.FindFirstValue(MunerAppClaims.NombreCompleto) ?? "",
            FechaAdopcion = a?.FechaAdopcion ?? DateTime.Today,
            Observaciones = a?.Observaciones
        });
    }

    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> Adoptante(int id, AdoptanteFormViewModel model)
    {
        var b = await _db.Beneficiarios.Include(x => x.Adoptante).FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();
        if (b.Estado != EstadoBeneficiario.Adoptado)
        {
            TempData["Error"] = "Los datos del adoptante solo se registran cuando el beneficiario está en estado \"Adoptado\".";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        if (model.FechaAdopcion is DateTime fecha)
        {
            if (fecha.Date > DateTime.Today)
                ModelState.AddModelError(nameof(model.FechaAdopcion), "La fecha de adopción no puede ser futura.");
            else if (fecha.Date < b.FechaRescate.Date)
                ModelState.AddModelError(nameof(model.FechaAdopcion), $"La fecha de adopción no puede ser anterior al ingreso ({Formatos.Fecha(b.FechaRescate)}).");
        }

        model.BeneficiarioId = b.Id;
        model.NombreBeneficiario = b.Nombre;
        model.FechaRescate = b.FechaRescate;
        model.YaRegistrado = b.Adoptante is not null;
        if (!ModelState.IsValid) return View(model);

        var a = b.Adoptante;
        if (a is null)
        {
            a = new AdoptanteBeneficiario { BeneficiarioId = b.Id, EsalId = b.EsalId };
            _db.AdoptantesBeneficiario.Add(a);
        }
        a.Nombre = model.Nombre.Trim();
        a.Documento = Limpiar(model.Documento);
        a.Telefono = model.Telefono.Trim();
        a.Correo = string.IsNullOrWhiteSpace(model.Correo) ? null : model.Correo.Trim();
        a.Ciudad = model.Ciudad.Trim();
        a.Direccion = Limpiar(model.Direccion);
        a.NumeroFormulario = Limpiar(model.NumeroFormulario);
        a.Elaboro = model.Elaboro.Trim();
        a.FechaAdopcion = model.FechaAdopcion!.Value.Date;
        a.Observaciones = string.IsNullOrWhiteSpace(model.Observaciones) ? null : model.Observaciones.Trim();
        a.RegistradoPorId = UsuarioId;
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"Guardaste los datos del adoptante de {b.Nombre}.";
        return RedirectToAction(nameof(Detalle), new { id });
    }

    // ---------- HU-020: apadrinamiento (lo que ve el público) ----------

    [HttpGet]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> Apadrinamiento(int id)
    {
        var b = await _db.Beneficiarios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();
        if (!await _db.DatosDonacion.AnyAsync()) return SinDatosParaDonar();
        if (!PuedeApadrinarse(b.Estado))
        {
            TempData["Error"] = $"{b.Nombre} está en estado \"{Textos.De(b.Estado)}\" y no puede ofrecerse para apadrinar.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        return View(await FormularioApadrinamientoAsync(b, new ApadrinamientoFormViewModel
        {
            HistoriaPublica = b.HistoriaPublica ?? "",
            AporteSugerido = b.AporteSugerido is decimal aporte ? ((long)aporte).ToString("N0", new System.Globalization.CultureInfo("es-CO")) : ""
        }));
    }

    // Escenario 1: marcar como apadrinable con foto, historia corta y aporte sugerido
    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    [RequestSizeLimit(ValidadorArchivos.LimitePeticionBytes)]
    public async Task<IActionResult> Apadrinamiento(int id, ApadrinamientoFormViewModel model)
    {
        var b = await _db.Beneficiarios.FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();
        if (!await _db.DatosDonacion.AnyAsync()) return SinDatosParaDonar();
        if (!PuedeApadrinarse(b.Estado))
        {
            TempData["Error"] = $"{b.Nombre} está en estado \"{Textos.De(b.Estado)}\" y no puede ofrecerse para apadrinar.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        var aporte = Formatos.LeerPesos(model.AporteSugerido);
        if (!string.IsNullOrWhiteSpace(model.AporteSugerido))
        {
            if (aporte is null) ModelState.AddModelError(nameof(model.AporteSugerido), "Escribe el valor solo con números, por ejemplo 30.000.");
            else if (aporte < 5_000) ModelState.AddModelError(nameof(model.AporteSugerido), "El aporte sugerido mínimo es $5.000.");
            else if (aporte > 5_000_000) ModelState.AddModelError(nameof(model.AporteSugerido), "El aporte sugerido no puede superar $5.000.000.");
        }

        ArchivoValidado? foto = null;
        if (model.Foto is { Length: > 0 })
        {
            foto = await ValidadorArchivos.ValidarAsync(model.Foto, TipoArchivo.Imagen);
            if (!foto.Valido) ModelState.AddModelError(nameof(model.Foto), foto.Error!);
        }
        else if (b.FotoPublicaRuta is null && !(model.UsarFotoInterna && b.FotoRuta is not null))
        {
            ModelState.AddModelError(nameof(model.Foto), "Agrega la foto que verá el público" + (b.FotoRuta is not null ? " o marca la opción de usar la foto de la hoja de vida." : "."));
        }

        if (!ModelState.IsValid) return View(await FormularioApadrinamientoAsync(b, model));

        string? fotoAnterior = null;
        if (foto is { Valido: true })
        {
            fotoAnterior = b.FotoPublicaRuta;
            await using var stream = model.Foto!.OpenReadStream();
            b.FotoPublicaRuta = await _archivos.GuardarAsync(stream, $"esal/{b.EsalId}/apadrinables", foto.Extension, publico: true);
        }
        else if (b.FotoPublicaRuta is null && model.UsarFotoInterna && b.FotoRuta is not null)
        {
            // Copia de la foto interna al almacenamiento público (la interna sigue siendo privada)
            await using var origen = await _archivos.AbrirAsync(b.FotoRuta);
            if (origen is null)
            {
                ModelState.AddModelError(nameof(model.Foto), "No se pudo leer la foto de la hoja de vida. Sube una nueva.");
                return View(await FormularioApadrinamientoAsync(b, model));
            }
            b.FotoPublicaRuta = await _archivos.GuardarAsync(origen, $"esal/{b.EsalId}/apadrinables", Path.GetExtension(b.FotoRuta), publico: true);
        }

        var yaEraApadrinable = b.Apadrinable;
        b.Apadrinable = true;
        b.HistoriaPublica = model.HistoriaPublica.Trim();
        b.AporteSugerido = aporte;
        b.FechaApadrinable ??= DateTime.UtcNow;
        await _db.SaveChangesAsync();
        if (fotoAnterior is not null) await _archivos.EliminarAsync(fotoAnterior);

        TempData["Mensaje"] = yaEraApadrinable
            ? $"Actualizaste la información pública de {b.Nombre}."
            : $"{b.Nombre} ya aparece en la opción \"Apadrinar\" del perfil de la fundación.";
        return RedirectToAction(nameof(Detalle), new { id });
    }

    // Escenario 3: retirar del apadrinamiento
    [HttpPost]
    [Authorize(Policy = Politicas.AdminEsalPrincipal)]
    public async Task<IActionResult> RetirarApadrinamiento(int id)
    {
        var b = await _db.Beneficiarios.FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();
        if (!b.Apadrinable)
        {
            TempData["Error"] = $"{b.Nombre} no estaba ofrecido para apadrinar.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        await RetirarDeApadrinamientoAsync(b);
        var avisados = await NotificarPadrinosAsync(b, $"{b.Nombre} ya no se ofrece para apadrinar",
            $"La fundación dejó de ofrecer a {b.Nombre} para apadrinar. Tu apadrinamiento sigue activo hasta que decidas cancelarlo.");
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"{b.Nombre} dejó de mostrarse al público." +
            (avisados > 0 ? $" Avisamos a {avisados} {(avisados == 1 ? "padrino" : "padrinos")}." : "");
        return RedirectToAction(nameof(Detalle), new { id });
    }

    /// <summary>Sin cuenta oficial para recibir aportes, el padrino no tendría a dónde transferir.</summary>
    private IActionResult SinDatosParaDonar()
    {
        TempData["Error"] = "Antes de ofrecer beneficiarios para apadrinar, configura los datos para donar: es la cuenta donde los padrinos harán sus aportes.";
        return RedirectToAction("Index", "DatosDonacion");
    }

    private static bool PuedeApadrinarse(EstadoBeneficiario estado)
        => !ReglasBeneficiario.EsSalida(estado);

    /// <summary>Avisa a los padrinos activos del beneficiario (se guarda con el siguiente SaveChanges). Devuelve cuántos son.</summary>
    private async Task<int> NotificarPadrinosAsync(Beneficiario b, string titulo, string mensaje)
    {
        var padrinos = await _db.Apadrinamientos.AsNoTracking()
            .Where(a => a.BeneficiarioId == b.Id && a.Estado == EstadoApadrinamiento.Activo)
            .Select(a => new { a.Id, a.PadrinoId })
            .ToListAsync();
        foreach (var p in padrinos)
            _notificaciones.Agregar(p.PadrinoId, titulo, mensaje, $"/mis-apadrinamientos/{p.Id}", "bi-balloon-heart");
        return padrinos.Count;
    }

    /// <summary>Deja de mostrarse al público; el texto y el aporte se conservan por si se vuelve a ofrecer.</summary>
    private async Task RetirarDeApadrinamientoAsync(Beneficiario b)
    {
        b.Apadrinable = false;
        b.FechaApadrinable = null;
        if (b.FotoPublicaRuta is not null)
        {
            await _archivos.EliminarAsync(b.FotoPublicaRuta); // la copia pública ya no debe poder abrirse
            b.FotoPublicaRuta = null;
        }
    }

    private async Task<ApadrinamientoFormViewModel> FormularioApadrinamientoAsync(Beneficiario b, ApadrinamientoFormViewModel model)
    {
        model.BeneficiarioId = b.Id;
        model.NombreBeneficiario = b.Nombre;
        model.YaApadrinable = b.Apadrinable;
        model.TieneFotoInterna = b.FotoRuta is not null;
        model.FotoPublicaUrl = b.FotoPublicaRuta is null ? null : _archivos.UrlPublica(b.FotoPublicaRuta);
        model.SlugEsal = await _db.Esales.AsNoTracking().Where(e => e.Id == b.EsalId).Select(e => e.Slug).FirstOrDefaultAsync();
        return model;
    }

    // ---------- Apoyo ----------

    private async Task<ArchivoValidado?> ValidarAsync(BeneficiarioFormViewModel model)
    {
        if (model.FechaRescate is DateTime rescate && rescate.Date > DateTime.Today)
            ModelState.AddModelError(nameof(model.FechaRescate), "La fecha de ingreso no puede ser futura.");

        if (model.FechaNacimiento is DateTime nacimiento)
        {
            if (nacimiento.Date > DateTime.Today)
                ModelState.AddModelError(nameof(model.FechaNacimiento), "La fecha de nacimiento no puede ser futura.");
            else if (nacimiento.Date < DateTime.Today.AddYears(-30))
                ModelState.AddModelError(nameof(model.FechaNacimiento), "Revisa la fecha de nacimiento: es de hace más de 30 años.");
            else if (model.FechaRescate is DateTime ingreso && nacimiento.Date > ingreso.Date)
                ModelState.AddModelError(nameof(model.FechaNacimiento), "La fecha de nacimiento no puede ser posterior al ingreso.");
        }

        if (!string.IsNullOrWhiteSpace(model.PesoIngreso))
        {
            var peso = Formatos.LeerDecimal(model.PesoIngreso);
            if (peso is null || peso <= 0 || peso > 100)
                ModelState.AddModelError(nameof(model.PesoIngreso), "Escribe el peso en kilos, por ejemplo 3,5.");
        }

        if (model.Foto is null || model.Foto.Length == 0) return null;
        var foto = await ValidadorArchivos.ValidarAsync(model.Foto, TipoArchivo.Imagen);
        if (!foto.Valido) ModelState.AddModelError(nameof(model.Foto), foto.Error!);
        return foto.Valido ? foto : null;
    }

    private static DateTime Nacimiento(BeneficiarioFormViewModel m)
        => m.FechaNacimiento?.Date ?? DateTime.Today.AddYears(-m.EdadAnios).AddMonths(-m.EdadMeses);

    /// <summary>Datos de la reseña del paciente (formato "Historia clínica e ingreso" de la fundación).</summary>
    private static void AplicarResena(Beneficiario b, BeneficiarioFormViewModel m)
    {
        b.Especie = "Felino"; // los beneficiarios de la fundación siempre son gatos
        b.Raza = Limpiar(m.Raza);
        b.PesoIngresoKg = Formatos.LeerDecimal(m.PesoIngreso);
        b.Procedencia = m.Procedencia;
        b.DetallesProcedencia = Limpiar(m.DetallesProcedencia);
        b.EstadoReproductivo = m.EstadoReproductivo;
        b.SenalesParticulares = Limpiar(m.SenalesParticulares);
    }

    private static string? Limpiar(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
