using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;

namespace MunerApp.Tests;

/// <summary>Trazabilidad: cada cambio guardado deja un registro con quién, cuándo y qué cambió.</summary>
public class AuditoriaTests
{
    private sealed class Persona(string? id, string? nombre) : IUsuarioActual
    {
        public string? Id => id;
        public string? Nombre => nombre;
    }

    private static MunerAppDbContext Contexto(IUsuarioActual? usuario) =>
        new(new DbContextOptionsBuilder<MunerAppDbContext>()
            .UseInMemoryDatabase("auditoria-" + Guid.NewGuid())
            .AddInterceptors(new AuditoriaInterceptor(usuario))
            .Options);

    [Fact]
    public async Task CrearModificarYEliminar_DejanUnRegistroCadaUno()
    {
        await using var db = Contexto(new Persona("u-1", "Marcela Quintero"));
        var causa = new Causa { EsalId = 7, Titulo = "Techo nuevo", Descripcion = "-", Meta = 1_000_000 };
        db.Causas.Add(causa);
        await db.SaveChangesAsync();

        causa.Meta = 1_500_000;
        await db.SaveChangesAsync();

        db.Causas.Remove(causa);
        await db.SaveChangesAsync();

        var registros = await db.RegistrosAuditoria.OrderBy(r => r.Id).ToListAsync();
        Assert.Equal(new[] { AccionAuditoria.Creado, AccionAuditoria.Modificado, AccionAuditoria.Eliminado }, registros.Select(r => r.Accion));
        Assert.All(registros, r =>
        {
            Assert.Equal("Causa", r.Entidad);
            Assert.Equal(causa.Id.ToString(), r.EntidadId);
            Assert.Equal(7, r.EsalId);
            Assert.Equal("u-1", r.UsuarioId);
            Assert.Equal("Marcela Quintero", r.UsuarioNombre);
        });
    }

    [Fact]
    public async Task AlModificar_GuardaSoloLosCamposQueCambiaron()
    {
        await using var db = Contexto(new Persona("u-1", "Admin"));
        var causa = new Causa { EsalId = 1, Titulo = "Vacunas", Descripcion = "-", Meta = 500_000 };
        db.Causas.Add(causa);
        await db.SaveChangesAsync();

        causa.Meta = 800_000;
        await db.SaveChangesAsync();

        var cambio = await db.RegistrosAuditoria.SingleAsync(r => r.Accion == AccionAuditoria.Modificado);
        var campos = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(cambio.Cambios)!;
        Assert.Equal(new[] { "Meta" }, campos.Keys);
        Assert.Equal(500_000m, campos["Meta"][0].GetDecimal());
        Assert.Equal(800_000m, campos["Meta"][1].GetDecimal());
    }

    [Fact]
    public async Task SinSesion_ElCambioQuedaACargoDelSistema()
    {
        await using var db = Contexto(usuario: null);
        db.Causas.Add(new Causa { EsalId = 1, Titulo = "Revisión diaria", Descripcion = "-" });
        await db.SaveChangesAsync();

        var registro = await db.RegistrosAuditoria.SingleAsync();
        Assert.Null(registro.UsuarioId);
        Assert.Equal("Sistema", registro.UsuarioNombre);
    }

    [Fact]
    public async Task LosSecretosCifrados_NoSeCopianAlRegistro()
    {
        await using var db = Contexto(new Persona("u-1", "Admin"));
        db.ConfigPasarelas.Add(new ConfigPasarela { EsalId = 3, LlavePublica = "pub_test", SecretoIntegridadCifrado = "XYZ-SECRETO", SecretoEventosCifrado = "ABC-SECRETO" });
        await db.SaveChangesAsync();

        var registro = await db.RegistrosAuditoria.SingleAsync();
        Assert.DoesNotContain("SECRETO", registro.Cambios);
        Assert.Contains("pub_test", registro.Cambios);
    }

    [Fact]
    public async Task CambiarSoloLosSaltosDeLinea_NoEsUnCambio()
    {
        await using var db = Contexto(new Persona("u-1", "Admin"));
        var causa = new Causa { EsalId = 1, Titulo = "Texto", Descripcion = "Línea uno\nLínea dos", Meta = 100 };
        db.Causas.Add(causa);
        await db.SaveChangesAsync();

        causa.Descripcion = "Línea uno\r\nLínea dos"; // así llega desde el formulario del navegador
        await db.SaveChangesAsync();

        var registros = await db.RegistrosAuditoria.ToListAsync();
        Assert.Single(registros);
        Assert.Contains("Línea uno", registros[0].Cambios); // tildes legibles, sin í
    }

    [Fact]
    public async Task UnGuardadoSinCambiosReales_NoDejaRegistro()
    {
        await using var db = Contexto(new Persona("u-1", "Admin"));
        var causa = new Causa { EsalId = 1, Titulo = "Igual", Descripcion = "-", Meta = 100 };
        db.Causas.Add(causa);
        await db.SaveChangesAsync();

        causa.Meta = 100; // mismo valor
        db.Entry(causa).Property(c => c.Meta).IsModified = true;
        await db.SaveChangesAsync();

        Assert.Equal(1, await db.RegistrosAuditoria.CountAsync());
    }
}
