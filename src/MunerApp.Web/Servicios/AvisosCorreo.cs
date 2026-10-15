using MunerApp.Application.Interfaces;
using MunerApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MunerApp.Web.Servicios;

/// <summary>
/// Envía por correo los avisos importantes, además de la notificación dentro de la plataforma
/// (revisión de flujos, Sprint 3). Si el correo no está configurado o falla, no se interrumpe la acción.
/// </summary>
public class AvisosCorreo
{
    private readonly ICorreoService _correo;
    private readonly MunerAppDbContext _db;
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<AvisosCorreo> _logger;

    public AvisosCorreo(ICorreoService correo, MunerAppDbContext db, IHttpContextAccessor http, ILogger<AvisosCorreo> logger)
    {
        _correo = correo;
        _db = db;
        _http = http;
        _logger = logger;
    }

    /// <summary>Envía el aviso al correo del usuario. <paramref name="urlRelativa"/> se convierte en enlace completo.</summary>
    public async Task EnviarAUsuarioAsync(string usuarioId, string asunto, string titulo, string mensaje, string textoBoton, string urlRelativa)
    {
        var correo = await _db.Users.Where(u => u.Id == usuarioId).Select(u => u.Email).FirstOrDefaultAsync();
        if (!string.IsNullOrWhiteSpace(correo))
            await EnviarAsync(correo, asunto, titulo, mensaje, textoBoton, urlRelativa);
    }

    public async Task EnviarAsync(string correo, string asunto, string titulo, string mensaje, string textoBoton, string urlRelativa)
    {
        try
        {
            await _correo.EnviarAsync(correo, asunto, PlantillasCorreo.Aviso(titulo, mensaje, textoBoton, Absoluta(urlRelativa)));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo enviar el aviso por correo a {Correo}: {Asunto}", correo, asunto);
        }
    }

    private string Absoluta(string urlRelativa)
    {
        var req = _http.HttpContext?.Request;
        return req is null ? urlRelativa : $"{req.Scheme}://{req.Host}{req.PathBase}{urlRelativa}";
    }
}
