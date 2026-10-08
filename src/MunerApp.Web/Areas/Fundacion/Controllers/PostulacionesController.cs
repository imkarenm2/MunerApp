using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Constantes;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Areas.Fundacion.Models;
using MunerApp.Web.Validacion;

namespace MunerApp.Web.Areas.Fundacion.Controllers;

/// <summary>HU-035: la fundación consulta las postulaciones. La aprobación se implementa en el Sprint 6.</summary>
[Area("Fundacion")]
[Authorize(Roles = Roles.AdministradorESAL)]
public class PostulacionesController : Controller
{
    private readonly MunerAppDbContext _db;
    private readonly IAlmacenamientoArchivos _archivos;

    public PostulacionesController(MunerAppDbContext db, IAlmacenamientoArchivos archivos)
    {
        _db = db;
        _archivos = archivos;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var postulaciones = await (from p in _db.PostulacionesVoluntario.AsNoTracking()
                                   join u in _db.Users on p.UsuarioId equals u.Id
                                   orderby p.FechaPostulacion descending
                                   select new PostulacionEsalItem
                                   {
                                       Id = p.Id,
                                       Nombre = u.NombreCompleto,
                                       Correo = u.Email ?? "",
                                       Telefono = p.Telefono,
                                       Tipo = p.Tipo,
                                       Disponibilidad = p.Disponibilidad,
                                       Motivacion = p.Motivacion,
                                       Institucion = p.Institucion,
                                       Programa = p.Programa,
                                       Semestre = p.Semestre,
                                       TieneSoporte = p.SoporteAcademicoRuta != null,
                                       Estado = p.Estado,
                                       Fecha = p.FechaPostulacion
                                   }).Take(200).ToListAsync();
        return View(postulaciones);
    }

    [HttpGet]
    public async Task<IActionResult> Soporte(int id)
    {
        var p = await _db.PostulacionesVoluntario.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (p?.SoporteAcademicoRuta is null) return NotFound();
        var stream = await _archivos.AbrirAsync(p.SoporteAcademicoRuta);
        return stream is null ? NotFound() : File(stream, ValidadorArchivos.ContentTypeDe(p.SoporteAcademicoRuta));
    }
}
