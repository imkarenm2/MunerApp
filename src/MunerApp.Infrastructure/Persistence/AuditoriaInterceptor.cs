using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Common;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;

namespace MunerApp.Infrastructure.Persistence;

/// <summary>
/// Trazabilidad (requerimiento no funcional): cada vez que se guardan cambios, deja un RegistroAuditoria por cada
/// dato creado, modificado o eliminado, con quién, cuándo y qué campos cambiaron. Funciona para toda la aplicación
/// sin tocar los controladores. No guarda contraseñas, sellos de seguridad, secretos ni el texto de los chats.
/// </summary>
public sealed class AuditoriaInterceptor : SaveChangesInterceptor
{
    /// <summary>Datos que no se auditan: el propio registro, bitácoras técnicas y mensajes privados.</summary>
    private static readonly HashSet<Type> Excluidos = new()
    {
        typeof(RegistroAuditoria), typeof(Notificacion), typeof(RevisionDiaria), typeof(EventoPasarela), typeof(MensajeTienda),
        typeof(IdentityUserToken<string>), typeof(IdentityUserLogin<string>), typeof(IdentityUserClaim<string>), typeof(IdentityRoleClaim<string>)
    };

    /// <summary>Campos que nunca se copian al registro (credenciales y secretos) o que solo hacen ruido.</summary>
    private static readonly string[] CamposOcultos =
        { "Password", "SecurityStamp", "ConcurrencyStamp", "Cifrado", "Secreto", "Token", "AccessFailedCount", "Normalized" };

    private const int LargoMaximo = 300;

    private readonly IUsuarioActual? _usuario;
    private List<(EntityEntry Entrada, RegistroAuditoria Registro)>? _pendientes;
    private bool _guardandoRegistros;

    public AuditoriaInterceptor(IUsuarioActual? usuario = null) => _usuario = usuario;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capturar(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Capturar(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (Completar(eventData.Context) is { } contexto)
        {
            try { contexto.SaveChanges(); }
            finally { _guardandoRegistros = false; }
        }
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        if (Completar(eventData.Context) is { } contexto)
        {
            try { await contexto.SaveChangesAsync(cancellationToken); }
            finally { _guardandoRegistros = false; }
        }
        return result;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => _pendientes = null;

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _pendientes = null;
        return Task.CompletedTask;
    }

    /// <summary>Antes de guardar: anota qué cambió (después ya no se sabe cuáles eran los valores anteriores).</summary>
    private void Capturar(DbContext? contexto)
    {
        if (contexto is null || _guardandoRegistros) return;

        var usuarioId = _usuario?.Id;
        var usuarioNombre = usuarioId is null ? "Sistema" : (_usuario!.Nombre ?? "Usuario");
        _pendientes = contexto.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted && !Excluidos.Contains(e.Metadata.ClrType))
            .Select(e => (e, new RegistroAuditoria
            {
                UsuarioId = usuarioId,
                UsuarioNombre = usuarioNombre,
                Entidad = NombreEntidad(e),
                Accion = e.State switch
                {
                    EntityState.Added => AccionAuditoria.Creado,
                    EntityState.Deleted => AccionAuditoria.Eliminado,
                    _ => AccionAuditoria.Modificado
                },
                Cambios = Cambios(e)
            }))
            .Where(p => p.Item2.Accion != AccionAuditoria.Modificado || p.Item2.Cambios != "{}")
            .ToList();
    }

    /// <summary>Después de guardar: ya existen los Id de lo creado. Devuelve el contexto si hay registros por guardar.</summary>
    private DbContext? Completar(DbContext? contexto)
    {
        if (contexto is null || _guardandoRegistros || _pendientes is not { Count: > 0 } pendientes) return null;
        _pendientes = null;

        foreach (var (entrada, registro) in pendientes)
        {
            registro.EntidadId = string.Join(",", entrada.Properties.Where(p => p.Metadata.IsPrimaryKey()).Select(p => p.CurrentValue ?? p.OriginalValue));
            registro.EsalId = entrada.Entity switch
            {
                IPerteneceAEsal dato => dato.EsalId,
                Esal esal => esal.Id,
                _ => null
            };
        }
        contexto.Set<RegistroAuditoria>().AddRange(pendientes.Select(p => p.Registro));
        _guardandoRegistros = true;
        return contexto;
    }

    private static string NombreEntidad(EntityEntry e) => e.Entity switch
    {
        IdentityUserRole<string> => "RolDeUsuario",
        IdentityRole => "Rol",
        _ => e.Metadata.ClrType.Name
    };

    private static string Cambios(EntityEntry e)
    {
        var propiedades = e.Properties.Where(p => !CamposOcultos.Any(o => p.Metadata.Name.Contains(o, StringComparison.OrdinalIgnoreCase)));
        Dictionary<string, object?> cambios = e.State switch
        {
            EntityState.Added => propiedades.Where(p => p.CurrentValue is not null && !p.Metadata.IsPrimaryKey())
                .ToDictionary(p => p.Metadata.Name, p => Valor(p.CurrentValue)),
            EntityState.Deleted => propiedades.Where(p => p.OriginalValue is not null)
                .ToDictionary(p => p.Metadata.Name, p => Valor(p.OriginalValue)),
            _ => propiedades.Where(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue))
                .ToDictionary(p => p.Metadata.Name, p => (object?)new[] { Valor(p.OriginalValue), Valor(p.CurrentValue) })
        };
        return JsonSerializer.Serialize(cambios);
    }

    /// <summary>Valores legibles y cortos: fechas ISO, enums por nombre, textos recortados.</summary>
    private static object? Valor(object? v) => v switch
    {
        null => null,
        DateTime f => f.ToString("yyyy-MM-dd HH:mm:ss"),
        Enum en => en.ToString(),
        string s when s.Length > LargoMaximo => s[..LargoMaximo] + "…",
        string or bool or int or long or decimal or double => v,
        _ => v.ToString()
    };
}
