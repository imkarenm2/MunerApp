using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MunerApp.Domain.Common;
using MunerApp.Domain.Constantes;
using MunerApp.Infrastructure.Identity;

namespace MunerApp.Infrastructure.Persistence;

/// <summary>Crea los roles y el superadministrador (credenciales en user-secrets / Azure, nunca en el repo).</summary>
public static class DataSeeder
{
    public static async Task SembrarAsync(IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DataSeeder");

        try
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var rol in Roles.Todos)
            {
                if (!await roleManager.RoleExistsAsync(rol))
                    await roleManager.CreateAsync(new IdentityRole(rol));
            }

            await AsignarSlugsAsync(scope.ServiceProvider.GetRequiredService<MunerAppDbContext>());

            var email = config["Seed:SuperAdmin:Email"];
            var password = config["Seed:SuperAdmin:Password"];
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning("No se configuró Seed:SuperAdmin; no se creó el superadministrador.");
                return;
            }

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
            if (await userManager.FindByEmailAsync(email) is null)
            {
                var admin = new Usuario
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    NombreCompleto = "Superadministrador MunerApp"
                };
                var resultado = await userManager.CreateAsync(admin, password);
                if (resultado.Succeeded)
                    await userManager.AddToRoleAsync(admin, Roles.SuperAdministrador);
                else
                    logger.LogError("No se pudo crear el superadministrador: {Errores}",
                        string.Join("; ", resultado.Errors.Select(e => e.Description)));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al sembrar datos iniciales. ¿Ya aplicaron las migraciones (dotnet ef database update)?");
        }
    }

    /// <summary>Las fundaciones registradas antes del Sprint 2 no tienen dirección pública; se la genera aquí.</summary>
    private static async Task AsignarSlugsAsync(MunerAppDbContext db)
    {
        var sinSlug = await db.Esales.Where(e => e.Slug == null || e.Slug == "").ToListAsync();
        if (sinSlug.Count == 0) return;

        var usados = new HashSet<string>(await db.Esales.Where(e => e.Slug != null).Select(e => e.Slug!).ToListAsync());
        foreach (var esal in sinSlug)
        {
            esal.Slug = SlugUnico(Slugs.Generar(esal.Nombre), usados);
            usados.Add(esal.Slug);
        }
        await db.SaveChangesAsync();
    }

    public static string SlugUnico(string baseSlug, ISet<string> usados)
    {
        var slug = baseSlug;
        for (var i = 2; usados.Contains(slug); i++) slug = $"{baseSlug}-{i}";
        return slug;
    }
}
