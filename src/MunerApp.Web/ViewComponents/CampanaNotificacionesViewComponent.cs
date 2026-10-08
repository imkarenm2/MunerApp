using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MunerApp.Application.Interfaces;

namespace MunerApp.Web.ViewComponents;

/// <summary>Campana del menú con el número de notificaciones sin leer.</summary>
public class CampanaNotificacionesViewComponent : ViewComponent
{
    private readonly INotificacionService _notificaciones;

    public CampanaNotificacionesViewComponent(INotificacionService notificaciones) => _notificaciones = notificaciones;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var id = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
        var cantidad = id is null ? 0 : await _notificaciones.ContarNoLeidasAsync(id);
        return View(cantidad);
    }
}
