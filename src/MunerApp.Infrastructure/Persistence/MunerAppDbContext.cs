using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Common;
using MunerApp.Domain.Constantes;
using MunerApp.Domain.Entities;
using MunerApp.Infrastructure.Identity;

namespace MunerApp.Infrastructure.Persistence;

public class MunerAppDbContext : IdentityDbContext<Usuario, IdentityRole, string>
{
    private readonly IEsalActual? _esalActual;

    public MunerAppDbContext(DbContextOptions<MunerAppDbContext> options, IEsalActual? esalActual = null)
        : base(options)
    {
        _esalActual = esalActual;
    }

    public DbSet<Esal> Esales => Set<Esal>();
    public DbSet<Modulo> Modulos => Set<Modulo>();
    public DbSet<EsalModulo> EsalModulos => Set<EsalModulo>();
    public DbSet<RedSocial> RedesSociales => Set<RedSocial>();
    public DbSet<ConfigPasarela> ConfigPasarelas => Set<ConfigPasarela>();

    // ---- Filtro multi-entidad (documento de diseño, sección 3) ----
    // Usuarios de una ESAL (administradores y voluntarios) solo ven datos de su fundación.
    // Superadministrador, tareas internas (seed, migraciones) y usuarios sin ESAL no se filtran:
    // las páginas públicas filtran explícitamente por la ESAL que se está consultando,
    // y los datos internos se protegen además con autorización por rol.
    private int? EsalIdActual => _esalActual?.EsalId;
    private bool SinFiltroEsal => _esalActual is null || _esalActual.EsSuperAdmin || _esalActual.EsalId is null;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Usuario>(e =>
        {
            e.Property(x => x.NombreCompleto).HasMaxLength(150).IsRequired();
            e.Property(x => x.Perfil).HasMaxLength(20);
            e.HasOne(x => x.Esal).WithMany().HasForeignKey(x => x.EsalId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Esal>(e =>
        {
            e.ToTable("Esal");
            e.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
            e.Property(x => x.Nit).HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.Nit).IsUnique();
            e.Property(x => x.TipoEntidad).HasMaxLength(50).IsRequired();
            e.Property(x => x.CorreoContacto).HasMaxLength(150).IsRequired();
        });

        builder.Entity<Modulo>(e =>
        {
            e.ToTable("Modulo");
            e.Property(x => x.Codigo).HasMaxLength(30).IsRequired();
            e.HasIndex(x => x.Codigo).IsUnique();
            e.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
            e.HasData(
                new Modulo { Id = 1, Codigo = CodigosModulo.Perfil, Nombre = "Perfil y transparencia", EsConfigurable = false },
                new Modulo { Id = 2, Codigo = CodigosModulo.Donaciones, Nombre = "Donaciones y causas", EsConfigurable = false },
                new Modulo { Id = 3, Codigo = CodigosModulo.Voluntariado, Nombre = "Voluntariado", EsConfigurable = false },
                new Modulo { Id = 4, Codigo = CodigosModulo.Boletin, Nombre = "Boletín", EsConfigurable = false },
                new Modulo { Id = 5, Codigo = CodigosModulo.Beneficiarios, Nombre = "Beneficiarios y apadrinamiento", EsConfigurable = true },
                new Modulo { Id = 6, Codigo = CodigosModulo.Tienda, Nombre = "Tienda", EsConfigurable = true },
                new Modulo { Id = 7, Codigo = CodigosModulo.Adopcion, Nombre = "Adopción", EsConfigurable = true },
                new Modulo { Id = 8, Codigo = CodigosModulo.Salud, Nombre = "Salud e insumos", EsConfigurable = true });
        });

        builder.Entity<EsalModulo>(e =>
        {
            e.ToTable("EsalModulo");
            e.HasKey(x => new { x.EsalId, x.ModuloId });
            e.HasOne(x => x.Esal).WithMany(x => x.Modulos).HasForeignKey(x => x.EsalId);
            e.HasOne(x => x.Modulo).WithMany().HasForeignKey(x => x.ModuloId);
        });

        builder.Entity<RedSocial>(e =>
        {
            e.ToTable("RedSocial");
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Url).HasMaxLength(300).IsRequired();
            e.HasIndex(x => new { x.EsalId, x.Tipo }).IsUnique();
            e.HasOne(x => x.Esal).WithMany(x => x.RedesSociales).HasForeignKey(x => x.EsalId);
        });

        builder.Entity<ConfigPasarela>(e =>
        {
            e.ToTable("ConfigPasarela");
            e.HasKey(x => x.EsalId);
            e.Property(x => x.Proveedor).HasMaxLength(20).IsRequired();
            e.Property(x => x.LlavePublica).HasMaxLength(100).IsRequired();
            e.Property(x => x.Ambiente).HasConversion<string>().HasMaxLength(10);
            e.HasOne(x => x.Esal).WithOne(x => x.ConfigPasarela).HasForeignKey<ConfigPasarela>(x => x.EsalId);
        });

        // Filtro por ESAL en cada entidad que pertenece a una fundación.
        // Al crear una entidad nueva con EsalId, agréguenla aquí.
        AplicarFiltroEsal<EsalModulo>(builder);
        AplicarFiltroEsal<RedSocial>(builder);
        AplicarFiltroEsal<ConfigPasarela>(builder);
    }

    private void AplicarFiltroEsal<T>(ModelBuilder builder) where T : class, IPerteneceAEsal
    {
        builder.Entity<T>().HasQueryFilter(e => SinFiltroEsal || e.EsalId == EsalIdActual);
    }
}
