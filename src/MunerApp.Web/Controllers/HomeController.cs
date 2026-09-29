using Microsoft.AspNetCore.Mvc;

namespace MunerApp.Web.Controllers;

public class HomeController : Controller
{
    private readonly IWebHostEnvironment _entorno;

    public HomeController(IWebHostEnvironment entorno) => _entorno = entorno;

    public IActionResult Index() => View();

    /// <summary>Guía de estilos para el equipo. Solo disponible en desarrollo.</summary>
    public IActionResult Estilos() => _entorno.IsDevelopment() ? View() : NotFound();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();
}
