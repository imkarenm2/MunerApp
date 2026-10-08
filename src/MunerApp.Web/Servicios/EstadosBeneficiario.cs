using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Constantes;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;

namespace MunerApp.Web.Servicios;

/// <summary>
/// Cambios de estado de un beneficiario con sus efectos (HU-017, HU-020): historial con fecha,
/// retiro del apadrinamiento y aviso a los padrinos. Lo usan la hoja de vida (Sprint 3) y la
/// adopción concretada en la cita (HU-034), para que las reglas estén en un solo lugar.
/// Los cambios se guardan con el siguiente SaveChanges.
/// </summary>
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

    /// <summary>Un beneficiario adoptado o fallecido ya no se ofrece para apadrinar (HU-020).</summary>
    public static bool PuedeApadrinarse(EstadoBeneficiario estado)
        => estado is not (EstadoBeneficiario.Adoptado or EstadoBeneficiario.Fallecido);

    /// <summary>Cambia el estado y lo registra en el historial. Devuelve true si dejó de ofrecerse para apadrinar.</summary>
    public async Task<bool> CambiarAsync(Beneficiario b, EstadoBeneficiario nuevo, string? usuarioId, string? nota)
    {
        b.Estado = nuevo;

        var retiradoDeApadrinamiento = b.Apadrinable && !PuedeApadrinarse(nuevo);
        if (retiradoDeApadrinamiento)
        {
            await RetirarDeApadrinamientoAsync(b);
            await NotificarPadrinosAsync(b, $"{b.Nombre} cambió de estado",
                $"{b.Nombre} ahora está \"{Textos.De(nuevo)}\" y dejó de ofrecerse para apadrinar. Tu apadrinamiento sigue activo hasta que decidas cancelarlo.");
        }
        _db.HistorialEstadosBeneficiario.Add(new HistorialEstadoBeneficiario
        {
            EsalId = b.EsalId,
            BeneficiarioId = b.Id,
            Estado = nuevo,
            CambiadoPorId = usuarioId,
            Nota = string.IsNullOrWhiteSpace(nota) ? null : nota.Trim()
        });
        return retiradoDeApadrinamiento;
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
