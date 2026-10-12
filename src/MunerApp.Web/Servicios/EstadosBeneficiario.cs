using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Constantes;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;

namespace MunerApp.Web.Servicios;

/// <summary>
/// Cambios de estado de un beneficiario con sus efectos (HU-017, HU-020): historial con fecha,
/// fin de los apadrinamientos cuando el gato sale de la fundación y aviso a los padrinos. Lo usan la hoja de vida (Sprint 3) y la
/// adopción concretada en la cita (HU-034), para que las reglas estén en un solo lugar.
/// Los cambios se guardan con el siguiente SaveChanges.
/// </summary>
/// <summary>Qué pasó al cambiar el estado: si dejó de ofrecerse para apadrinar y cuántos apadrinamientos terminaron.</summary>
public record ResultadoCambioEstado(bool RetiradoDeApadrinamiento, int ApadrinamientosFinalizados);

public class EstadosBeneficiario
{
    private readonly MunerAppDbContext _db;
    private readonly IAlmacenamientoArchivos _archivos;
    private readonly INotificacionService _notificaciones;

    public EstadosBeneficiario(MunerAppDbContext db, IAlmacenamientoArchivos archivos, INotificacionService notificaciones)
    {
        _db = db;
        _archivos = archivos;
        _notificaciones = notificaciones;
    }

    /// <summary>Un gato que salió de la fundación (adoptado, liberado, encontró su hogar o falleció) ya no se ofrece para apadrinar (HU-020).</summary>
    public static bool PuedeApadrinarse(EstadoBeneficiario estado)
        => !ReglasBeneficiario.EsSalida(estado);

    /// <summary>Cambia el estado y lo registra en el historial. Devuelve true si dejó de ofrecerse para apadrinar.</summary>
    public async Task<bool> CambiarAsync(Beneficiario b, EstadoBeneficiario nuevo, string? usuarioId, string? nota)
        => (await CambiarConResultadoAsync(b, nuevo, usuarioId, nota)).RetiradoDeApadrinamiento;

    /// <summary>
    /// Cambia el estado y aplica sus efectos. Si el gato sale de la fundación, sus apadrinamientos activos terminan
    /// y se avisa a cada padrino (ver README, "Reglas de los flujos").
    /// </summary>
    public async Task<ResultadoCambioEstado> CambiarConResultadoAsync(Beneficiario b, EstadoBeneficiario nuevo, string? usuarioId, string? nota)
    {
        b.Estado = nuevo;

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
                    (nuevo == EstadoBeneficiario.Fallecido ? "" : " Puedes apadrinar a otro gato de la fundación."),
                    $"/mis-apadrinamientos/{a.Id}", icono);
            }
            finalizados = activos.Count;
        }

        var retiradoDeApadrinamiento = b.Apadrinable && !PuedeApadrinarse(nuevo);
        if (retiradoDeApadrinamiento)
            await RetirarDeApadrinamientoAsync(b);

        _db.HistorialEstadosBeneficiario.Add(new HistorialEstadoBeneficiario
        {
            EsalId = b.EsalId,
            BeneficiarioId = b.Id,
            Estado = nuevo,
            CambiadoPorId = usuarioId,
            Nota = string.IsNullOrWhiteSpace(nota) ? null : nota.Trim()
        });
        return new ResultadoCambioEstado(retiradoDeApadrinamiento, finalizados);
    }

    /// <summary>Avisa a los padrinos activos del beneficiario. Devuelve cuántos son.</summary>
    public async Task<int> NotificarPadrinosAsync(Beneficiario b, string titulo, string mensaje)
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
    public async Task RetirarDeApadrinamientoAsync(Beneficiario b)
    {
        b.Apadrinable = false;
        b.FechaApadrinable = null;
        if (b.FotoPublicaRuta is not null)
        {
            await _archivos.EliminarAsync(b.FotoPublicaRuta); // la copia pública ya no debe poder abrirse
            b.FotoPublicaRuta = null;
        }
    }
}
