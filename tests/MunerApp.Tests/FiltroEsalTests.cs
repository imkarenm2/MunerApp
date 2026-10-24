using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Entities;
using MunerApp.Infrastructure.Persistence;

namespace MunerApp.Tests;

/// <summary>
/// Filtro global por fundación (multi-ESAL): cada usuario ve solo lo de su fundación,
/// y quien no pertenece a ninguna no ve nada salvo que pueda ver todas (superadmin, veedor, tareas internas).
/// </summary>
public class FiltroEsalTests
{
    private sealed class UsuarioDePrueba(int? esalId, bool veTodas = false) : IEsalActual
    {
        public bool EstaAutenticado => true;
        public bool EsSuperAdmin => false;
        public int? EsalId => esalId;
        public bool VeTodasLasFundaciones => veTodas;
    }

    /// <summary>Una base en memoria con dos causas de la fundación 1 y una de la fundación 2.</summary>
    private static MunerAppDbContext Contexto(IEsalActual? usuario)
    {
        var opciones = new DbContextOptionsBuilder<MunerAppDbContext>()
            .UseInMemoryDatabase("filtro-" + Guid.NewGuid())
            .Options;

        using (var semilla = new MunerAppDbContext(opciones))
        {
            semilla.Causas.AddRange(
                new Causa { EsalId = 1, Titulo = "Causa A1", Descripcion = "-" },
                new Causa { EsalId = 1, Titulo = "Causa A2", Descripcion = "-" },
                new Causa { EsalId = 2, Titulo = "Causa B1", Descripcion = "-" });
            semilla.SaveChanges();
        }
        return new MunerAppDbContext(opciones, usuario);
    }

    [Fact]
    public void ElAdministradorDeUnaFundacion_SoloVeLasSuyas()
    {
        using var db = Contexto(new UsuarioDePrueba(esalId: 1));
        Assert.All(db.Causas.ToList(), c => Assert.Equal(1, c.EsalId));
        Assert.Equal(2, db.Causas.Count());
    }

    [Fact]
    public void UnAdministradorSinFundacion_NoVeNada()
        => Assert.Empty(Contexto(new UsuarioDePrueba(esalId: null)).Causas.ToList());

    [Fact]
    public void ElVeedor_VeTodasLasFundaciones()
        => Assert.Equal(3, Contexto(new UsuarioDePrueba(esalId: null, veTodas: true)).Causas.Count());

    [Fact]
    public void LasTareasInternasSinUsuario_VenTodo()
        => Assert.Equal(3, Contexto(usuario: null).Causas.Count());

    [Fact]
    public void LasPaginasPublicas_IgnoranElFiltro()
        => Assert.Equal(3, Contexto(new UsuarioDePrueba(esalId: null)).Causas.IgnoreQueryFilters().Count());
}
