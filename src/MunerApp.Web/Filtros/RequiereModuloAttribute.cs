using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MunerApp.Application.Interfaces;

namespace MunerApp.Web.Filtros;

/// <summary>
/// Protege un controlador o acción de un módulo configurable (HU-007).
/// Uso: [RequiereModulo(CodigosModulo.Tienda)]
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequiereModuloAttribute : TypeFilterAttribute
{
    public RequiereModuloAttribute(string codigoModulo) : base(typeof(RequiereModuloFilter))
    {
        Arguments = new object[] { codigoModulo };
    }
}

public class RequiereModuloFilter : IAsyncActionFilter
{
    private readonly string _codigoModulo;
    private readonly IEsalActual _esalActual;
    private readonly IModuloService _modulos;

    public RequiereModuloFilter(string codigoModulo, IEsalActual esalActual, IModuloService modulos)
    {
        _codigoModulo = codigoModulo;
        _esalActual = esalActual;
        _modulos = modulos;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (_esalActual.EsSuperAdmin)
        {
            await next();
            return;
        }

        // En páginas públicas la ESAL viene en la ruta (esalId); en los paneles, del usuario.
        int? esalId = _esalActual.EsalId;
        if (context.RouteData.Values.TryGetValue("esalId", out var valorRuta)
            && int.TryParse(valorRuta?.ToString(), out var idRuta))
        {
            esalId = idRuta;
        }

        if (esalId is null || !await _modulos.EstaActivoAsync(esalId.Value, _codigoModulo))
        {
            context.Result = new ViewResult
            {
                ViewName = "ModuloNoDisponible",
                StatusCode = StatusCodes.Status404NotFound
            };
            return;
        }

        await next();
    }
}
