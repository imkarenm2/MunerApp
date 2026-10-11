using Microsoft.EntityFrameworkCore;
using MunerApp.Infrastructure.Persistence;

namespace MunerApp.Web.Servicios;

/// <summary>Una tarea de la revisión diaria (HU-039: alertas de salud; HU-046: cierre de causas).</summary>
public interface ITareaDiaria
{
    /// <summary>Nombre único y estable: identifica la tarea en la tabla RevisionDiaria.</summary>
    string Nombre { get; }

    /// <summary>Hace la revisión del día <paramref name="hoy"/> (hora de Colombia). Devuelve un resumen corto.</summary>
    Task<string> EjecutarAsync(DateTime hoy, CancellationToken ct);
}

/// <summary>
/// Ejecuta una vez al día las tareas registradas como <see cref="ITareaDiaria"/>, a partir de la hora configurada
/// (RevisionDiaria:Hora, 6 a. m. por defecto, hora de Colombia). Revisa cada 30 minutos si ya toca, así que si la
/// aplicación estaba apagada a esa hora, la revisión corre apenas vuelva a encender ese mismo día.
/// En Azure App Service se debe activar "Always On" para que la aplicación no se duerma sin visitas.
/// </summary>
public class RevisionDiariaService : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(30);

    /// <summary>Si una ejecución quedó a medias (la aplicación se cayó), otra la puede retomar pasado este tiempo.</summary>
    private static readonly TimeSpan EjecucionAbandonada = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<RevisionDiariaService> _logger;
    private readonly bool _activa;
    private readonly int _hora;

    public RevisionDiariaService(IServiceScopeFactory scopes, ILogger<RevisionDiariaService> logger, IConfiguration config)
    {
        _scopes = scopes;
        _logger = logger;
        _activa = config.GetValue("RevisionDiaria:Activa", true);
        _hora = Math.Clamp(config.GetValue("RevisionDiaria:Hora", 6), 0, 23);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!_activa)
        {
            _logger.LogInformation("La revisión diaria está desactivada (RevisionDiaria:Activa = false).");
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), ct); // deja terminar el arranque (migraciones y datos iniciales)
            using var reloj = new PeriodicTimer(Intervalo);
            do
            {
                var ahora = Formatos.Local(DateTime.UtcNow);
                if (ahora.Hour >= _hora)
                    await EjecutarTareasAsync(ahora.Date, ct);
            } while (await reloj.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // La aplicación se está apagando
        }
    }

    private async Task EjecutarTareasAsync(DateTime hoy, CancellationToken ct)
    {
        List<string> nombres;
        using (var scope = _scopes.CreateScope())
            nombres = scope.ServiceProvider.GetServices<ITareaDiaria>().Select(t => t.Nombre).ToList();

        // Cada tarea en su propio scope: un error en una no impide las demás
        foreach (var nombre in nombres)
        {
            using var scope = _scopes.CreateScope();
            var tarea = scope.ServiceProvider.GetServices<ITareaDiaria>().First(t => t.Nombre == nombre);
            var db = scope.ServiceProvider.GetRequiredService<MunerAppDbContext>();
            try
            {
                await EjecutarUnaVezAsync(db, tarea, hoy, _logger, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Falló la tarea diaria {Tarea} del {Fecha:yyyy-MM-dd}", nombre, hoy);
            }
        }
    }

    /// <summary>
    /// Ejecuta la tarea si nadie la ha ejecutado hoy. El registro (fecha, tarea) se inserta antes de empezar:
    /// si otra instancia ya lo insertó, la llave primaria lo impide y esta no hace nada.
    /// Devuelve true si la ejecutó.
    /// </summary>
    public static async Task<bool> EjecutarUnaVezAsync(MunerAppDbContext db, ITareaDiaria tarea, DateTime hoy,
        ILogger logger, CancellationToken ct)
    {
        hoy = hoy.Date;
        if (!await TomarAsync(db, tarea.Nombre, hoy, ct)) return false;

        logger.LogInformation("Revisión diaria {Tarea} del {Fecha:yyyy-MM-dd}: inicia", tarea.Nombre, hoy);
        var resumen = await tarea.EjecutarAsync(hoy, ct);
        if (resumen.Length > 500) resumen = resumen[..500];

        var ahora = DateTime.UtcNow;
        await db.RevisionesDiarias.Where(r => r.Fecha == hoy && r.Tarea == tarea.Nombre)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Terminada, ahora).SetProperty(r => r.Resumen, resumen), ct);
        logger.LogInformation("Revisión diaria {Tarea} del {Fecha:yyyy-MM-dd}: {Resumen}", tarea.Nombre, hoy, resumen);
        return true;
    }

    private static async Task<bool> TomarAsync(MunerAppDbContext db, string tarea, DateTime hoy, CancellationToken ct)
    {
        var existente = await db.RevisionesDiarias.AsNoTracking().FirstOrDefaultAsync(r => r.Fecha == hoy && r.Tarea == tarea, ct);
        if (existente is null)
        {
            db.RevisionesDiarias.Add(new Domain.Entities.RevisionDiaria { Fecha = hoy, Tarea = tarea });
            try
            {
                await db.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear(); // otra instancia la tomó al mismo tiempo
                return false;
            }
        }

        // Ya terminó, o la está haciendo otra instancia
        if (existente.Terminada is not null || existente.Iniciada > DateTime.UtcNow - EjecucionAbandonada) return false;

        // Quedó a medias: se retoma (las marcas de cada aviso evitan repetir lo que ya se alcanzó a hacer)
        var ahora = DateTime.UtcNow;
        var retomada = await db.RevisionesDiarias
            .Where(r => r.Fecha == hoy && r.Tarea == tarea && r.Terminada == null && r.Iniciada == existente.Iniciada)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Iniciada, ahora), ct);
        return retomada == 1;
    }
}
