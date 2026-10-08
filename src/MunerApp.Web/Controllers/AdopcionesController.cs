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
using MunerApp.Web.Validacion;

namespace MunerApp.Web.Controllers;

/// <summary>
/// Solicitud de adopción en línea. HU-029: la persona lee y acepta las recomendaciones
/// y responsabilidades; solo entonces se habilita el formulario por secciones (HU-030 a HU-032),
/// que guarda el avance en cada una.
/// </summary>
[Authorize]
public class AdopcionesController : Controller
{
    private readonly MunerAppDbContext _db;
    private readonly IAlmacenamientoArchivos _archivos;
    private readonly IModuloService _modulos;
    private readonly IEsalActual _esalActual;
    private readonly INotificacionService _notificaciones;

    public AdopcionesController(MunerAppDbContext db, IAlmacenamientoArchivos archivos, IModuloService modulos, IEsalActual esalActual,
        INotificacionService notificaciones)
    {
        _db = db;
        _archivos = archivos;
        _modulos = modulos;
        _esalActual = esalActual;
        _notificaciones = notificaciones;
    }

    private string UsuarioId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // ---------- HU-029: recomendaciones y responsabilidades ----------

    /// <summary>Cualquier visitante puede leer las recomendaciones; para aceptarlas debe iniciar sesión (escenario 3).</summary>
    [AllowAnonymous]
    [HttpGet("fundaciones/{slug}/adoptar")]
    public async Task<IActionResult> Recomendaciones(string slug)
    {
        var esal = await BuscarConAdopcionAsync(slug);
        if (esal is null) return ModuloNoDisponible();
        return View(await PrepararAsync(new RecomendacionesAdopcionViewModel(), esal));
    }

    [HttpPost("fundaciones/{slug}/adoptar")]
    public async Task<IActionResult> Recomendaciones(string slug, RecomendacionesAdopcionViewModel model)
    {
        var esal = await BuscarConAdopcionAsync(slug);
        if (esal is null) return ModuloNoDisponible();

        var acepto = model.Acepto;
        model = await PrepararAsync(model, esal);
        if (model.Aviso is not null) return View(model);

        // Escenario 2: sin aceptar no se accede al formulario
        if (!acepto)
        {
            ModelState.AddModelError(nameof(model.Acepto), "Para continuar debes leer y aceptar las recomendaciones y responsabilidades.");
            return View(model);
        }

        // Escenario 1: la aceptación queda registrada y habilita el formulario
        var borrador = await BuscarBorradorAsync(esal.Id);
        if (borrador is null)
        {
            borrador = new SolicitudAdopcion { EsalId = esal.Id, UsuarioId = UsuarioId };
            _db.SolicitudesAdopcion.Add(borrador);
        }
        borrador.FechaAceptacionRecomendaciones = DateTime.UtcNow;
        borrador.FechaActualizacion = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Formulario), new { slug });
    }

    // ---------- HU-030 a HU-032: formulario por secciones ----------

    /// <summary>Lleva a la persona a la sección donde quedó su solicitud.</summary>
    [HttpGet("fundaciones/{slug}/adoptar/formulario")]
    public async Task<IActionResult> Formulario(string slug)
    {
        var (_, borrador, salida) = await AbrirSeccionAsync(slug, 1);
        if (salida is not null) return salida;

        return borrador!.SeccionesCompletadas switch
        {
            0 => RedirectToAction(nameof(Datos), new { slug }),
            1 => RedirectToAction(nameof(Mascotas), new { slug }),
            _ => RedirectToAction(nameof(Hogar), new { slug })
        };
    }

    // ---------- HU-030: sección 1, datos personales y de contacto ----------

    [HttpGet("fundaciones/{slug}/adoptar/formulario/datos")]
    public async Task<IActionResult> Datos(string slug)
    {
        var (esal, s, salida) = await AbrirSeccionAsync(slug, 1);
        if (salida is not null) return salida;

        var model = new DatosPersonalesAdopcionViewModel
        {
            NombreCompleto = s!.NombreCompleto ?? User.FindFirstValue(MunerAppClaims.NombreCompleto) ?? "",
            Cedula = s.Cedula ?? "",
            Edad = s.Edad,
            Celular = s.Celular ?? "",
            Ciudad = s.Ciudad ?? "",
            Direccion = s.Direccion ?? "",
            Ocupacion = s.Ocupacion,
            DetalleOcupacion = s.DetalleOcupacion,
            ReferenciaNombre = s.ReferenciaNombre ?? "",
            ReferenciaCelular = s.ReferenciaCelular ?? "",
            ReferenciaRelacion = s.ReferenciaRelacion ?? ""
        };
        return View(PrepararSeccion(model, esal!, s, 1));
    }

    [HttpPost("fundaciones/{slug}/adoptar/formulario/datos")]
    public async Task<IActionResult> Datos(string slug, DatosPersonalesAdopcionViewModel model)
    {
        var (esal, s, salida) = await AbrirSeccionAsync(slug, 1);
        if (salida is not null) return salida;

        // Escenario 3: cédula y celulares con formato válido (la edad mínima la valida el modelo)
        var cedula = ValidadorPersonas.Cedula(model.Cedula);
        if (!string.IsNullOrWhiteSpace(model.Cedula) && cedula is null)
            ModelState.AddModelError(nameof(model.Cedula), ValidadorPersonas.ErrorCedula);

        var celular = ValidadorPersonas.Celular(model.Celular);
        if (!string.IsNullOrWhiteSpace(model.Celular) && celular is null)
            ModelState.AddModelError(nameof(model.Celular), ValidadorPersonas.ErrorCelular);

        var celularReferencia = ValidadorPersonas.Celular(model.ReferenciaCelular);
        if (!string.IsNullOrWhiteSpace(model.ReferenciaCelular) && celularReferencia is null)
            ModelState.AddModelError(nameof(model.ReferenciaCelular), ValidadorPersonas.ErrorCelular);
        else if (celularReferencia is not null && celularReferencia == celular)
            ModelState.AddModelError(nameof(model.ReferenciaCelular), "La referencia debe ser otra persona: escribe un celular diferente al tuyo.");

        if (!string.IsNullOrEmpty(model.ReferenciaRelacion) && !DatosPersonalesAdopcionViewModel.Relaciones.Contains(model.ReferenciaRelacion))
            ModelState.AddModelError(nameof(model.ReferenciaRelacion), "Selecciona una relación de la lista.");

        // Escenario 2: si es independiente, debe indicar a qué se dedica
        if (model.Ocupacion == OcupacionAdoptante.Independiente && string.IsNullOrWhiteSpace(model.DetalleOcupacion))
            ModelState.AddModelError(nameof(model.DetalleOcupacion), "Cuéntanos a qué te dedicas como independiente.");

        if (!ModelState.IsValid) return View(PrepararSeccion(model, esal!, s!, 1));

        // Escenario 1: se guarda el avance y se pasa a la sección de mascotas
        s!.NombreCompleto = model.NombreCompleto.Trim();
        s.Cedula = cedula;
        s.Edad = model.Edad;
        s.Celular = celular;
        s.Ciudad = model.Ciudad.Trim();
        s.Direccion = model.Direccion.Trim();
        s.Ocupacion = model.Ocupacion;
        s.DetalleOcupacion = model.Ocupacion == OcupacionAdoptante.Independiente ? model.DetalleOcupacion!.Trim() : null;
        s.ReferenciaNombre = model.ReferenciaNombre.Trim();
        s.ReferenciaCelular = celularReferencia;
        s.ReferenciaRelacion = model.ReferenciaRelacion;
        s.SeccionesCompletadas = Math.Max(s.SeccionesCompletadas, 1);
        s.FechaActualizacion = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = "Guardamos tus datos personales. Sigue con la sección de mascotas.";
        return RedirectToAction(nameof(Mascotas), new { slug });
    }

    // ---------- HU-031: sección 2, mascotas ----------

    [HttpGet("fundaciones/{slug}/adoptar/formulario/mascotas")]
    public async Task<IActionResult> Mascotas(string slug)
    {
        var (esal, s, salida) = await AbrirSeccionAsync(slug, 2);
        if (salida is not null) return salida;

        var model = new MascotasAdopcionViewModel
        {
            Mascotas = s!.Mascotas,
            TieneGato = s.TieneGato,
            TienePerro = s.TienePerro,
            TieneOtraMascota = s.TieneOtraMascota,
            OtraMascota = s.OtraMascota,
            GatoUsaArenero = s.GatoUsaArenero,
            GatoEsterilizacion = s.GatoEsterilizacion,
            GatoVacunas = s.GatoVacunas,
            PerroSociabilidad = s.PerroSociabilidad,
            QuePasoMascota = s.QuePasoMascota
        };
        return View(PrepararSeccion(model, esal!, s, 2));
    }

    [HttpPost("fundaciones/{slug}/adoptar/formulario/mascotas")]
    [RequestSizeLimit(ValidadorArchivos.LimitePeticionBytes)]
    public async Task<IActionResult> Mascotas(string slug, MascotasAdopcionViewModel model)
    {
        var (esal, s, salida) = await AbrirSeccionAsync(slug, 2);
        if (salida is not null) return salida;

        var tiene = model.Mascotas == TenenciaMascotas.Tengo;
        var gato = tiene && model.TieneGato;
        var perro = tiene && model.TienePerro;
        var otra = tiene && model.TieneOtraMascota;

        // Escenario 1: si tiene mascotas, el tipo y las preguntas de cada tipo
        if (tiene && !model.TieneGato && !model.TienePerro && !model.TieneOtraMascota)
            ModelState.AddModelError(nameof(model.TieneGato), "Selecciona qué mascotas tienes.");
        if (otra && string.IsNullOrWhiteSpace(model.OtraMascota))
            ModelState.AddModelError(nameof(model.OtraMascota), "Cuéntanos qué otra mascota tienes.");
        if (gato)
        {
            if (model.GatoUsaArenero is null)
                ModelState.AddModelError(nameof(model.GatoUsaArenero), "Indica si tu gato usa arenero.");
            if (model.GatoEsterilizacion is null)
                ModelState.AddModelError(nameof(model.GatoEsterilizacion), "Indica si tus gatos están esterilizados.");
            if (model.GatoVacunas is null)
                ModelState.AddModelError(nameof(model.GatoVacunas), "Indica si tus gatos están vacunados.");
        }
        if (perro && model.PerroSociabilidad is null)
            ModelState.AddModelError(nameof(model.PerroSociabilidad), "Indica si tu perro es sociable con los gatos.");

        // Escenario 3: si tuvo mascotas, qué ocurrió con ellas
        if (model.Mascotas == TenenciaMascotas.Tuve && string.IsNullOrWhiteSpace(model.QuePasoMascota))
            ModelState.AddModelError(nameof(model.QuePasoMascota), "Cuéntanos qué ocurrió con tu mascota.");

        // Escenario 2: carné opcional, solo con vacunas completas o parciales (opciones a y c)
        var permiteCarne = gato && MascotasAdopcionViewModel.PermiteCarne(model.GatoVacunas);
        ArchivoValidado? carne = null;
        if (permiteCarne && model.CarneVacunas is { Length: > 0 })
        {
            carne = await ValidadorArchivos.ValidarAsync(model.CarneVacunas, TipoArchivo.Documento);
            if (!carne.Valido) ModelState.AddModelError(nameof(model.CarneVacunas), carne.Error!);
        }

        if (!ModelState.IsValid) return View(PrepararSeccion(model, esal!, s!, 2));

        // El carné anterior se elimina si lo reemplaza, lo quita o ya no aplica
        if (s!.CarneVacunasRuta is not null && (carne is not null || model.QuitarCarne || !permiteCarne))
        {
            await _archivos.EliminarAsync(s.CarneVacunasRuta);
            s.CarneVacunasRuta = null;
        }
        if (carne is not null)
        {
            await using var stream = model.CarneVacunas!.OpenReadStream();
            s.CarneVacunasRuta = await _archivos.GuardarAsync(stream, $"esal/{esal!.Id}/adopciones", carne.Extension, publico: false);
        }

        // Solo se guardan las respuestas que aplican según las preguntas anteriores
        s.Mascotas = model.Mascotas;
        s.TieneGato = gato;
        s.TienePerro = perro;
        s.TieneOtraMascota = otra;
        s.OtraMascota = otra ? model.OtraMascota!.Trim() : null;
        s.GatoUsaArenero = gato ? model.GatoUsaArenero : null;
        s.GatoEsterilizacion = gato ? model.GatoEsterilizacion : null;
        s.GatoVacunas = gato ? model.GatoVacunas : null;
        s.PerroSociabilidad = perro ? model.PerroSociabilidad : null;
        s.QuePasoMascota = model.Mascotas == TenenciaMascotas.Tuve ? model.QuePasoMascota!.Trim() : null;
        s.SeccionesCompletadas = Math.Max(s.SeccionesCompletadas, 2);
        s.FechaActualizacion = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = "Guardamos la sección de mascotas. Ya casi terminas: falta la sección de hogar y compromisos.";
        return RedirectToAction(nameof(Hogar), new { slug });
    }

    /// <summary>El solicitante descarga el carné que adjuntó (archivo privado).</summary>
    [HttpGet("fundaciones/{slug}/adoptar/formulario/carne")]
    public async Task<IActionResult> Carne(string slug)
    {
        var (_, s, salida) = await AbrirSeccionAsync(slug, 2);
        if (salida is not null) return salida;
        if (s!.CarneVacunasRuta is null) return NotFound();

        var stream = await _archivos.AbrirAsync(s.CarneVacunasRuta);
        return stream is null ? NotFound() : File(stream, ValidadorArchivos.ContentTypeDe(s.CarneVacunasRuta));
    }

    // ---------- HU-032: sección 3, hogar y compromisos ----------

    [HttpGet("fundaciones/{slug}/adoptar/formulario/hogar")]
    public async Task<IActionResult> Hogar(string slug)
    {
        var (esal, s, salida) = await AbrirSeccionAsync(slug, 3);
        if (salida is not null) return salida;

        var model = new HogarAdopcionViewModel
        {
            TipoVivienda = s!.TipoVivienda,
            TenenciaVivienda = s.TenenciaVivienda,
            ArrendadorPermiteMascotas = s.ArrendadorPermiteMascotas,
            Convivientes = s.Convivientes,
            ConvivientesDeAcuerdo = s.ConvivientesDeAcuerdo,
            NinosEnCasa = s.NinosEnCasa,
            NinosInteractuanMascotas = s.NinosInteractuanMascotas,
            EmbarazoEnHogar = s.EmbarazoEnHogar,
            PuedeCubrirCostos = s.PuedeCubrirCostos,
            AceptaVisita = s.AceptaVisita
        };
        return View(await PrepararHogarAsync(model, esal!, s));
    }

    /// <summary>Guarda la sección 3 y envía la solicitud a la fundación (escenario 3).</summary>
    [HttpPost("fundaciones/{slug}/adoptar/formulario/hogar")]
    public async Task<IActionResult> Hogar(string slug, HogarAdopcionViewModel model)
    {
        var (esal, s, salida) = await AbrirSeccionAsync(slug, 3);
        if (salida is not null) return salida;

        var arrendada = model.TenenciaVivienda == TenenciaVivienda.Arrendada;
        if (arrendada && model.ArrendadorPermiteMascotas is null)
            ModelState.AddModelError(nameof(model.ArrendadorPermiteMascotas), "Indica si el arrendador permite mascotas.");

        // Escenario 1: si hay niños, si han interactuado con mascotas
        if (model.NinosEnCasa == true && model.NinosInteractuanMascotas is null)
            ModelState.AddModelError(nameof(model.NinosInteractuanMascotas), "Indica si los niños han interactuado con mascotas.");

        // Escenario 3: requisito, contrato y autorización de datos son obligatorios para enviar
        if (!model.AceptaRequisito)
            ModelState.AddModelError(nameof(model.AceptaRequisito), "Para enviar la solicitud debes aceptar el requisito de la adopción.");
        if (!model.AceptaContrato)
            ModelState.AddModelError(nameof(model.AceptaContrato), "Para enviar la solicitud debes aceptar la firma del contrato de adopción.");
        if (!model.AutorizaDatos)
            ModelState.AddModelError(nameof(model.AutorizaDatos), "Para enviar la solicitud debes autorizar el tratamiento de tus datos personales.");

        if (!ModelState.IsValid) return View(await PrepararHogarAsync(model, esal!, s!));

        s!.TipoVivienda = model.TipoVivienda;
        s.TenenciaVivienda = model.TenenciaVivienda;
        s.ArrendadorPermiteMascotas = arrendada ? model.ArrendadorPermiteMascotas : null;
        s.Convivientes = model.Convivientes;
        s.ConvivientesDeAcuerdo = model.ConvivientesDeAcuerdo;
        s.NinosEnCasa = model.NinosEnCasa;
        s.NinosInteractuanMascotas = model.NinosEnCasa == true ? model.NinosInteractuanMascotas : null;
        s.EmbarazoEnHogar = model.EmbarazoEnHogar;
        s.PuedeCubrirCostos = model.PuedeCubrirCostos;
        s.AceptaVisita = model.AceptaVisita;
        s.AceptaRequisito = true;
        s.AceptaContrato = true;
        s.AutorizaDatos = true;
        s.FechaAutorizacionDatos = DateTime.UtcNow;
        s.SeccionesCompletadas = SeccionAdopcionViewModel.TotalSecciones;

        // La solicitud pasa a "Recibida" con su código único
        s.Estado = EstadoSolicitudAdopcion.Recibida;
        s.FechaEnvio = DateTime.UtcNow;
        s.FechaActualizacion = DateTime.UtcNow;
        s.Codigo = $"ADO-{DateTime.UtcNow:yyyy}-{s.Id:D6}";

        await _notificaciones.AgregarAAdministradoresAsync(esal!.Id,
            "Nueva solicitud de adopción",
            $"{s.NombreCompleto} envió la solicitud de adopción {s.Codigo}.",
            "/Fundacion/Adopciones", "bi-house-heart");
        await _db.SaveChangesAsync();

        TempData["Mensaje"] = $"¡Listo! Enviaste tu solicitud de adopción {s.Codigo} a {esal.Nombre}. Quedó recibida y te avisaremos cuando la revisen.";
        return RedirectToAction(nameof(MisAdopciones));
    }

    // ---------- Mis adopciones: las solicitudes enviadas por la persona ----------

    [HttpGet("mis-adopciones")]
    public async Task<IActionResult> MisAdopciones()
    {
        var solicitudes = await _db.SolicitudesAdopcion.IgnoreQueryFilters().AsNoTracking()
            .Where(s => s.UsuarioId == UsuarioId && s.Estado != EstadoSolicitudAdopcion.Borrador)
            .OrderByDescending(s => s.FechaEnvio)
            .Select(s => new
            {
                s.Codigo, s.EsalId, s.Esal!.Nombre, s.Esal.Slug, s.Esal.LogoRuta, s.Estado, s.FechaEnvio, s.MotivoRechazo,
                s.FechaCita, s.LugarCita, s.IndicacionesCita, Adoptado = s.BeneficiarioAdoptado != null ? s.BeneficiarioAdoptado.Nombre : null
            })
            .ToListAsync();

        // Aporte de cada fundación, para los requisitos de la cita (HU-034, escenario 1)
        var esales = solicitudes.Select(s => s.EsalId).Distinct().ToList();
        var aportes = await _db.ConfigAdopciones.IgnoreQueryFilters().AsNoTracking()
            .Where(c => esales.Contains(c.EsalId))
            .ToDictionaryAsync(c => c.EsalId, c => c.ValorAporte);

        return View(solicitudes.Select(s => new SolicitudAdopcionItem
        {
            Codigo = s.Codigo,
            NombreEsal = s.Nombre,
            SlugEsal = s.Slug ?? "",
            LogoUrl = UrlArchivo(s.LogoRuta),
            Estado = s.Estado,
            Fecha = s.FechaEnvio ?? DateTime.UtcNow,
            MotivoRechazo = s.MotivoRechazo,
            FechaCita = s.FechaCita,
            LugarCita = s.LugarCita,
            IndicacionesCita = s.IndicacionesCita,
            ValorAporte = aportes.TryGetValue(s.EsalId, out var aporte) ? aporte : ConfigAdopcion.ValorAportePredeterminado,
            NombreAdoptado = s.Adoptado
        }).ToList());
    }

    /// <summary>Datos de la fundación para la sección 3: aporte e información de toxoplasmosis (escenario 2).</summary>
    private async Task<HogarAdopcionViewModel> PrepararHogarAsync(HogarAdopcionViewModel model, Esal esal, SolicitudAdopcion borrador)
    {
        var config = await _db.ConfigAdopciones.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(c => c.EsalId == esal.Id);
        model.ValorAporte = config?.ValorAporte ?? ConfigAdopcion.ValorAportePredeterminado;
        model.MensajeToxoplasmosis = string.IsNullOrWhiteSpace(config?.MensajeToxoplasmosis)
            ? ConfigAdopcion.MensajeToxoplasmosisPredeterminado
            : config.MensajeToxoplasmosis;
        model.ImagenToxoplasmosisUrl = UrlArchivo(config?.ImagenToxoplasmosisRuta);
        return PrepararSeccion(model, esal, borrador, 3);
    }

    /// <summary>
    /// Valida que se pueda abrir la sección: fundación con adopción activa, borrador creado al aceptar
    /// las recomendaciones (escenario 2 de HU-029) y secciones anteriores guardadas (no se saltan secciones).
    /// </summary>
    private async Task<(Esal? Esal, SolicitudAdopcion? Borrador, IActionResult? Salida)> AbrirSeccionAsync(string slug, int seccion)
    {
        var esal = await BuscarConAdopcionAsync(slug);
        if (esal is null) return (null, null, ModuloNoDisponible());

        var borrador = await BuscarBorradorAsync(esal.Id);
        if (borrador is null && await _db.SolicitudesAdopcion.IgnoreQueryFilters()
                .AnyAsync(s => s.EsalId == esal.Id && s.UsuarioId == UsuarioId && SolicitudAdopcion.EstadosEnProceso.Contains(s.Estado)))
        {
            // Ya envió su solicitud: no hay formulario que diligenciar
            TempData["Mensaje"] = $"Ya enviaste tu solicitud de adopción a {esal.Nombre}. Aquí puedes ver en qué va.";
            return (esal, null, RedirectToAction(nameof(MisAdopciones)));
        }
        if (borrador is null)
        {
            TempData["Error"] = "Antes de diligenciar el formulario debes leer y aceptar las recomendaciones.";
            return (esal, null, RedirectToAction(nameof(Recomendaciones), new { slug }));
        }

        if (seccion > borrador.SeccionesCompletadas + 1)
            return (esal, borrador, RedirectToAction(nameof(Formulario), new { slug }));

        return (esal, borrador, null);
    }

    private T PrepararSeccion<T>(T model, Esal esal, SolicitudAdopcion borrador, int seccion) where T : SeccionAdopcionViewModel
    {
        model.Slug = esal.Slug!;
        model.NombreEsal = esal.Nombre;
        model.LogoUrl = UrlArchivo(esal.LogoRuta);
        model.Seccion = seccion;
        model.SeccionesCompletadas = borrador.SeccionesCompletadas;
        if (model is DatosPersonalesAdopcionViewModel datos)
            datos.Correo = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "";
        if (model is MascotasAdopcionViewModel mascotas)
            mascotas.TieneCarne = borrador.CarneVacunasRuta is not null;
        return model;
    }

    /// <summary>Fundación activa con el módulo de adopción activo, o null.</summary>
    private async Task<Esal?> BuscarConAdopcionAsync(string slug)
    {
        var esal = await _db.Esales.AsNoTracking().FirstOrDefaultAsync(e => e.Slug == slug && e.Activa);
        return esal is not null && await _modulos.EstaActivoAsync(esal.Id, CodigosModulo.Adopcion) ? esal : null;
    }

    private Task<SolicitudAdopcion?> BuscarBorradorAsync(int esalId)
        => _db.SolicitudesAdopcion.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.EsalId == esalId && s.UsuarioId == UsuarioId && s.Estado == EstadoSolicitudAdopcion.Borrador);

    private async Task<RecomendacionesAdopcionViewModel> PrepararAsync(RecomendacionesAdopcionViewModel model, Esal esal)
    {
        var config = await _db.ConfigAdopciones.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(c => c.EsalId == esal.Id);

        model.Slug = esal.Slug!;
        model.NombreEsal = esal.Nombre;
        model.LogoUrl = UrlArchivo(esal.LogoRuta);
        model.Recomendaciones = ConfigAdopcion.ComoLista(config?.Recomendaciones);
        model.Acepto = false;

        if (User.Identity?.IsAuthenticated != true) return model;

        if (_esalActual.EsalId == esal.Id)
            model.Aviso = $"Haces parte del equipo de {esal.Nombre}: las solicitudes de adopción las hacen las personas interesadas desde su cuenta personal.";
        else if (_esalActual.EsalId is not null || _esalActual.EsSuperAdmin)
            model.Aviso = "Las cuentas de una fundación no pueden solicitar adopciones. Si quieres adoptar a título personal, crea una cuenta con tu correo personal.";
        else if (await _db.SolicitudesAdopcion.IgnoreQueryFilters()
                     .AnyAsync(s => s.EsalId == esal.Id && s.UsuarioId == UsuarioId && SolicitudAdopcion.EstadosEnProceso.Contains(s.Estado)))
            model.Aviso = $"Ya tienes una solicitud de adopción en proceso con {esal.Nombre}. Te avisaremos cuando la revisen.";
        else
            model.TieneBorrador = await BuscarBorradorAsync(esal.Id) is not null;

        return model;
    }

    private IActionResult ModuloNoDisponible()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("ModuloNoDisponible");
    }

    private string? UrlArchivo(string? clave) => string.IsNullOrEmpty(clave) ? null : _archivos.UrlPublica(clave);
}
