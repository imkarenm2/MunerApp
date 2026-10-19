using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Constantes;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;

namespace MunerApp.Web.Servicios;

/// <summary>
/// HU-046 escenario 1: en la revisión diaria, la causa que alcanzó su meta o pasó su fecha límite queda
/// "Cerrada" y deja de recibir donaciones. Se avisa a los administradores para que publiquen la rendición de cuentas.
/// Las consultas ignoran el filtro por ESAL porque la revisión diaria no tiene usuario.
/// </summary>
public class CierreCausas : ITareaDiaria
{
    private readonly MunerAppDbContext _db;
    private readonly INotificacionService _notificaciones;

    public CierreCausas(MunerAppDbContext db, INotificacionService notificaciones)
    {
        _db = db;
        _notificaciones = notificaciones;
    }

    public string Nombre => "CierreCausas";

    public async Task<string> EjecutarAsync(DateTime hoy, CancellationToken ct)
    {
        var abiertas = await _db.Causas.IgnoreQueryFilters()
            .Where(c => (c.Estado == EstadoCausa.Activa || c.Estado == EstadoCausa.Pausada) && c.Esal!.Activa) // las que siguen en revisión no se cierran
            .ToListAsync(ct);
        if (abiertas.Count == 0) return "0 causas cerradas";

        var ids = abiertas.Select(c => c.Id).ToList();
        var recaudos = await _db.Donaciones.IgnoreQueryFilters().AsNoTracking()
            .Where(d => d.CausaId != null && ids.Contains(d.CausaId.Value) && d.Estado == EstadoDonacion.Confirmada)
            .GroupBy(d => d.CausaId!.Value)
            .Select(g => new { g.Key, Total = g.Sum(x => x.Valor) })
            .ToDictionaryAsync(x => x.Key, x => x.Total, ct);

        int porMeta = 0, porFecha = 0;
        foreach (var c in abiertas)
        {
            var recaudado = recaudos.GetValueOrDefault(c.Id);
            if (c.MotivoDeCierre(recaudado, hoy) is not MotivoCierreCausa motivo) continue;
            await CerrarAsync(c, motivo, recaudado);
            if (motivo == MotivoCierreCausa.MetaAlcanzada) porMeta++; else porFecha++;
        }

        await _db.SaveChangesAsync(ct); // los cierres y los avisos se guardan juntos
        return $"{porMeta + porFecha} causas cerradas ({porMeta} por meta, {porFecha} por fecha límite)";
    }

    /// <summary>
    /// Cierra la causa (se guarda con el siguiente SaveChanges). Si la cerró la revisión diaria, avisa a los
    /// administradores; si la cerró un administrador (o la está cerrando al publicar la rendición), no hace falta.
    /// </summary>
    public async Task CerrarAsync(Causa c, MotivoCierreCausa motivo, decimal recaudado, bool avisar = true)
    {
        c.Estado = EstadoCausa.Cerrada;
        c.FechaCierre = DateTime.UtcNow;
        c.MotivoCierre = motivo;
        if (motivo == MotivoCierreCausa.Manual || !avisar) return;

        var detalle = motivo == MotivoCierreCausa.MetaAlcanzada
            ? $"¡Alcanzó su meta! Se recaudaron {Formatos.Pesos(recaudado)}."
            : $"Terminó su plazo con {Formatos.Pesos(recaudado)} de {Formatos.Pesos(c.Meta)}.";
        await _notificaciones.AgregarAAdministradoresAsync(c.EsalId, $"La causa \"{c.Titulo}\" se cerró",
            $"{detalle} Ya no recibe donaciones. Publica la rendición de cuentas para contarles a los donantes en qué se usó el dinero.",
            $"/Fundacion/Causas/Rendicion/{c.Id}", "bi-flag");
    }

    /// <summary>Texto corto del motivo de cierre (para la causa abierta que ya debería cerrarse, se calcula).</summary>
    public static string TextoMotivo(Causa c, decimal recaudado, DateTime hoy)
        => Textos.De(c.MotivoCierre ?? c.MotivoDeCierre(recaudado, hoy) ?? MotivoCierreCausa.Manual);
}
