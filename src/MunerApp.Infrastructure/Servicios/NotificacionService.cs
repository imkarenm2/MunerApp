using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Constantes;
using MunerApp.Domain.Entities;
using MunerApp.Infrastructure.Persistence;

namespace MunerApp.Infrastructure.Servicios;

public class NotificacionService : INotificacionService
{
    private readonly MunerAppDbContext _db;

    public NotificacionService(MunerAppDbContext db) => _db = db;

    public void Agregar(string usuarioId, string titulo, string mensaje, string? url = null, string icono = "bi-bell")
    {
        _db.Notificaciones.Add(new Notificacion
        {
            UsuarioId = usuarioId,
            Titulo = Recortar(titulo, 120),
            Mensaje = Recortar(mensaje, 400),
            Url = url,
            Icono = icono
        });
    }

    public async Task AgregarAAdministradoresAsync(int esalId, string titulo, string mensaje, string? url = null, string icono = "bi-bell")
    {
        var rolAdminId = await _db.Roles.Where(r => r.Name == Roles.AdministradorESAL).Select(r => r.Id).FirstOrDefaultAsync();
        var admins = await _db.Users
            .Where(u => u.EsalId == esalId && u.Activo
                        && _db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == rolAdminId))
            .Select(u => u.Id)
            .ToListAsync();

        foreach (var id in admins) Agregar(id, titulo, mensaje, url, icono);
    }

    public Task<int> ContarNoLeidasAsync(string usuarioId)
        => _db.Notificaciones.CountAsync(n => n.UsuarioId == usuarioId && !n.Leida);

    private static string Recortar(string texto, int max) => texto.Length <= max ? texto : texto[..(max - 1)] + "…";
}
