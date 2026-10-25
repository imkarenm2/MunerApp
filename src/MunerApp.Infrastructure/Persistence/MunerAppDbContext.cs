using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Common;
using MunerApp.Domain.Constantes;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
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

    // Sprint 2
    public DbSet<FotoEsal> FotosEsal => Set<FotoEsal>();
    public DbSet<DocumentoTransparencia> DocumentosTransparencia => Set<DocumentoTransparencia>();
    public DbSet<DatosDonacion> DatosDonacion => Set<DatosDonacion>();
    public DbSet<Donacion> Donaciones => Set<Donacion>();
    public DbSet<NecesidadEspecie> NecesidadesEspecie => Set<NecesidadEspecie>();
    public DbSet<DonacionEspecie> DonacionesEspecie => Set<DonacionEspecie>();
    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();
    public DbSet<PostulacionVoluntario> PostulacionesVoluntario => Set<PostulacionVoluntario>();

    // Sprint 3
    public DbSet<Beneficiario> Beneficiarios => Set<Beneficiario>();
    public DbSet<HistorialEstadoBeneficiario> HistorialEstadosBeneficiario => Set<HistorialEstadoBeneficiario>();
    public DbSet<AdoptanteBeneficiario> AdoptantesBeneficiario => Set<AdoptanteBeneficiario>();
    public DbSet<EventoClinico> EventosClinicos => Set<EventoClinico>();
    public DbSet<FotoEventoClinico> FotosEventoClinico => Set<FotoEventoClinico>();
    public DbSet<Apadrinamiento> Apadrinamientos => Set<Apadrinamiento>();
    public DbSet<Causa> Causas => Set<Causa>();
    public DbSet<FotoCausa> FotosCausa => Set<FotoCausa>();
    public DbSet<EvaluacionComportamiento> EvaluacionesComportamiento => Set<EvaluacionComportamiento>();

    // Sprint 5
    public DbSet<ConfigAdopcion> ConfigAdopciones => Set<ConfigAdopcion>();
    public DbSet<SolicitudAdopcion> SolicitudesAdopcion => Set<SolicitudAdopcion>();
    public DbSet<EventoPasarela> EventosPasarela => Set<EventoPasarela>();
    // Sprint 4
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<FotoProducto> FotosProducto => Set<FotoProducto>();
    public DbSet<ConversacionTienda> ConversacionesTienda => Set<ConversacionTienda>();
    public DbSet<MensajeTienda> MensajesTienda => Set<MensajeTienda>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<Publicacion> Publicaciones => Set<Publicacion>();

    // Sprint 6
    public DbSet<Medicamento> Medicamentos => Set<Medicamento>();
    public DbSet<MovimientoMedicamento> MovimientosMedicamento => Set<MovimientoMedicamento>();
    public DbSet<EventoAgenda> EventosAgenda => Set<EventoAgenda>();
    public DbSet<ConfigSalud> ConfigSalud => Set<ConfigSalud>();
    public DbSet<RevisionDiaria> RevisionesDiarias => Set<RevisionDiaria>();
    public DbSet<NovedadBeneficiario> NovedadesBeneficiario => Set<NovedadBeneficiario>();
    public DbSet<FotoNovedad> FotosNovedad => Set<FotoNovedad>();
    public DbSet<RendicionCausa> RendicionesCausa => Set<RendicionCausa>();
    public DbSet<ArchivoRendicion> ArchivosRendicion => Set<ArchivoRendicion>();

    /// <summary>Trazabilidad: lo escribe AuditoriaInterceptor; lo consultan el veedor y el superadministrador.</summary>
    public DbSet<RegistroAuditoria> RegistrosAuditoria => Set<RegistroAuditoria>();

    // Libro de fondos y destinación de la especie (ciclo de vida de la donación)
    public DbSet<Fondo> Fondos => Set<Fondo>();
    public DbSet<MovimientoFondo> MovimientosFondo => Set<MovimientoFondo>();
    public DbSet<SalidaEspecie> SalidasEspecie => Set<SalidaEspecie>();

    // ---- Filtro multi-entidad (documento de diseño, sección 3) ----
    // Usuarios de una ESAL (administradores y voluntarios) solo ven datos de su fundación.
    // Solo el superadministrador y las tareas internas (seed, migraciones, revisión diaria) ven todo.
    // Cualquier otro usuario sin ESAL (visitante, donante) no ve nada por defecto: las páginas públicas y las
    // de donantes consultan con IgnoreQueryFilters() y filtran explícitamente por la ESAL o por el usuario.
    // Así, si un administrador queda sin ESAL por error, no ve los datos de todas las fundaciones.
    private int? EsalIdActual => _esalActual?.EsalId;
    private bool SinFiltroEsal => _esalActual is null || _esalActual.VeTodasLasFundaciones;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Usuario>(e =>
        {
            e.Property(x => x.NombreCompleto).HasMaxLength(150).IsRequired();
            e.Property(x => x.Perfil).HasMaxLength(20);
            e.Property(x => x.Organizacion).HasMaxLength(150);
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

            // Perfil institucional (HU-009)
            e.Property(x => x.Slug).HasMaxLength(90);
            e.HasIndex(x => x.Slug).IsUnique(); // EF agrega el filtro "Slug IS NOT NULL"
            e.Property(x => x.DescripcionCorta).HasMaxLength(200);
            e.Property(x => x.Historia).HasMaxLength(4000);
            e.Property(x => x.Mision).HasMaxLength(1000);
            e.Property(x => x.Vision).HasMaxLength(1000);
            e.Property(x => x.Ciudad).HasMaxLength(100);
            e.Property(x => x.Telefono).HasMaxLength(20);
            e.Property(x => x.LogoRuta).HasMaxLength(300);
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

        // ---------------- Sprint 2 ----------------

        builder.Entity<FotoEsal>(e =>
        {
            e.ToTable("FotoEsal");
            e.Property(x => x.Ruta).HasMaxLength(300).IsRequired();
            e.HasOne(x => x.Esal).WithMany(x => x.Fotos).HasForeignKey(x => x.EsalId);
        });

        builder.Entity<DocumentoTransparencia>(e =>
        {
            e.ToTable("DocumentoTransparencia");
            e.Property(x => x.Titulo).HasMaxLength(150).IsRequired();
            e.Property(x => x.Categoria).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Descripcion).HasMaxLength(300);
            e.Property(x => x.Ruta).HasMaxLength(300).IsRequired();
            e.Property(x => x.NombreOriginal).HasMaxLength(200).IsRequired();
            e.Property(x => x.Extension).HasMaxLength(10).IsRequired();
            e.Property(x => x.PublicadoPorId).HasMaxLength(450);
            e.HasIndex(x => new { x.EsalId, x.Visible });
            e.HasOne(x => x.Esal).WithMany(x => x.Documentos).HasForeignKey(x => x.EsalId);
        });

        builder.Entity<DatosDonacion>(e =>
        {
            e.ToTable("DatosDonacion");
            e.HasKey(x => x.EsalId);
            e.Property(x => x.Titular).HasMaxLength(150).IsRequired();
            e.Property(x => x.DocumentoTitular).HasMaxLength(20).IsRequired();
            e.Property(x => x.Entidad).HasMaxLength(100).IsRequired();
            e.Property(x => x.TipoCuenta).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.TipoLlave).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Numero).HasMaxLength(60).IsRequired();
            e.Property(x => x.Instrucciones).HasMaxLength(500);
            e.HasOne(x => x.Esal).WithOne(x => x.DatosDonacion).HasForeignKey<DatosDonacion>(x => x.EsalId);
        });

        // Donaciones en especie
        builder.Entity<NecesidadEspecie>(e =>
        {
            e.ToTable("NecesidadEspecie");
            e.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
            e.Property(x => x.Categoria).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Unidad).HasMaxLength(40).IsRequired();
            e.Property(x => x.Descripcion).HasMaxLength(500);
            e.Property(x => x.CreadaPorId).HasMaxLength(450);
            e.HasIndex(x => new { x.EsalId, x.Activa });
            e.HasOne(x => x.Esal).WithMany().HasForeignKey(x => x.EsalId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<DonacionEspecie>(e =>
        {
            e.ToTable("DonacionEspecie");
            e.Property(x => x.Codigo).HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.Codigo).IsUnique();
            e.Property(x => x.DonanteId).HasMaxLength(450).IsRequired();
            e.Property(x => x.Articulo).HasMaxLength(120).IsRequired();
            e.Property(x => x.Categoria).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Unidad).HasMaxLength(40).IsRequired();
            e.Property(x => x.FormaEntrega).HasConversion<string>().HasMaxLength(15);
            e.Property(x => x.FechaEntregaEstimada).HasColumnType("date");
            e.Property(x => x.DireccionRecogida).HasMaxLength(200);
            e.Property(x => x.Mensaje).HasMaxLength(300);
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(15);
            e.Property(x => x.FechaVencimiento).HasColumnType("date");
            e.Property(x => x.ObservacionesRecepcion).HasMaxLength(500);
            e.Property(x => x.FotoRecepcionRuta).HasMaxLength(300);
            e.Property(x => x.RecibidaPorId).HasMaxLength(450);
            e.Property(x => x.MotivoNoRecibida).HasMaxLength(300);
            e.HasIndex(x => new { x.EsalId, x.Estado });
            e.HasIndex(x => x.DonanteId);
            e.HasOne(x => x.Esal).WithMany().HasForeignKey(x => x.EsalId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Necesidad).WithMany().HasForeignKey(x => x.NecesidadId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.DonanteId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Donacion>(e =>
        {
            e.ToTable("Donacion");
            e.Property(x => x.Codigo).HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.Codigo).IsUnique();
            e.Property(x => x.DonanteId).HasMaxLength(450).IsRequired();
            e.Property(x => x.Valor).HasPrecision(14, 2);
            e.Property(x => x.MedioPago).HasMaxLength(200).IsRequired();
            e.Property(x => x.ReferenciaPago).HasMaxLength(60);
            e.Property(x => x.Mensaje).HasMaxLength(300);
            e.Property(x => x.SoporteRuta).HasMaxLength(300).IsRequired();
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.MotivoRechazo).HasMaxLength(300);
            e.Property(x => x.RevisadoPorId).HasMaxLength(450);
            e.HasIndex(x => new { x.EsalId, x.Estado });
            e.HasIndex(x => x.DonanteId);
            e.HasOne(x => x.Esal).WithMany().HasForeignKey(x => x.EsalId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.DonanteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Apadrinamiento).WithMany().HasForeignKey(x => x.ApadrinamientoId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.ApadrinamientoId);
            e.HasOne(x => x.Causa).WithMany().HasForeignKey(x => x.CausaId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.CausaId);
            // Donación en línea con Wompi (HU-043, HU-044)
            e.Property(x => x.Origen).HasConversion<string>().HasMaxLength(10).HasDefaultValue(OrigenDonacion.Manual);
            e.Property(x => x.ReferenciaPasarela).HasMaxLength(60);
            e.Property(x => x.TransaccionPasarelaId).HasMaxLength(60);
            e.HasIndex(x => x.ReferenciaPasarela).IsUnique(); // EF agrega el filtro "IS NOT NULL"
            e.HasIndex(x => x.TransaccionPasarelaId).IsUnique();
        });

        builder.Entity<Notificacion>(e =>
        {
            e.ToTable("Notificacion");
            e.Property(x => x.UsuarioId).HasMaxLength(450).IsRequired();
            e.Property(x => x.Titulo).HasMaxLength(120).IsRequired();
            e.Property(x => x.Mensaje).HasMaxLength(400).IsRequired();
            e.Property(x => x.Url).HasMaxLength(300);
            e.Property(x => x.Icono).HasMaxLength(40).IsRequired();
            e.HasIndex(x => new { x.UsuarioId, x.Leida });
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PostulacionVoluntario>(e =>
        {
            e.ToTable("PostulacionVoluntario");
            e.Property(x => x.UsuarioId).HasMaxLength(450).IsRequired();
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Telefono).HasMaxLength(20).IsRequired();
            e.Property(x => x.Disponibilidad).HasMaxLength(300).IsRequired();
            e.Property(x => x.Motivacion).HasMaxLength(500);
            e.Property(x => x.Institucion).HasMaxLength(150);
            e.Property(x => x.Programa).HasMaxLength(150);
            e.Property(x => x.SoporteAcademicoRuta).HasMaxLength(300);
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.MotivoRechazo).HasMaxLength(300);
            e.Property(x => x.RevisadoPorId).HasMaxLength(450);
            e.HasIndex(x => new { x.EsalId, x.UsuarioId, x.Estado });
            e.HasOne(x => x.Esal).WithMany().HasForeignKey(x => x.EsalId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------------- Sprint 3 ----------------

        builder.Entity<Beneficiario>(e =>
        {
            e.ToTable("Beneficiario");
            e.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
            e.Property(x => x.FechaNacimiento).HasColumnType("date");
            e.Property(x => x.FechaRescate).HasColumnType("date");
            e.Property(x => x.Sexo).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.Color).HasMaxLength(60).IsRequired();
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.FotoRuta).HasMaxLength(300);
            e.Property(x => x.RegistradoPorId).HasMaxLength(450);
            e.Property(x => x.HistoriaPublica).HasMaxLength(600);
            e.Property(x => x.AporteSugerido).HasPrecision(14, 2);
            e.Property(x => x.FotoPublicaRuta).HasMaxLength(300);

            // Reseña y examen de ingreso (formato de la fundación)
            e.Property(x => x.Especie).HasMaxLength(40).IsRequired().HasDefaultValue("Felino");
            e.Property(x => x.Raza).HasMaxLength(60);
            e.Property(x => x.PesoIngresoKg).HasPrecision(5, 2);
            e.Property(x => x.Procedencia).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.DetallesProcedencia).HasMaxLength(500);
            e.Property(x => x.EstadoReproductivo).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.SenalesParticulares).HasMaxLength(300);
            e.Property(x => x.Tllc).HasPrecision(4, 1);
            e.Property(x => x.Rpc).HasPrecision(4, 1);
            e.Property(x => x.Temperatura).HasPrecision(4, 1);
            e.Property(x => x.MucosaConjuntival).HasMaxLength(100);
            e.Property(x => x.MucosaOral).HasMaxLength(100);
            e.Property(x => x.MucosaRectal).HasMaxLength(100);
            e.Property(x => x.MucosaVulvarPrepucial).HasMaxLength(100);
            e.Property(x => x.EstadoConciencia).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ObservacionesIngreso).HasMaxLength(1000);
            e.Property(x => x.ExamenIngresoPorId).HasMaxLength(450);
            e.HasIndex(x => new { x.EsalId, x.Estado });
            e.HasIndex(x => new { x.EsalId, x.Apadrinable });
            e.HasOne(x => x.Esal).WithMany().HasForeignKey(x => x.EsalId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<HistorialEstadoBeneficiario>(e =>
        {
            e.ToTable("HistorialEstadoBeneficiario");
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.CambiadoPorId).HasMaxLength(450);
            e.Property(x => x.Nota).HasMaxLength(300);
            e.HasIndex(x => x.BeneficiarioId);
            e.HasOne(x => x.Beneficiario).WithMany(x => x.HistorialEstados).HasForeignKey(x => x.BeneficiarioId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AdoptanteBeneficiario>(e =>
        {
            e.ToTable("AdoptanteBeneficiario");
            e.HasKey(x => x.BeneficiarioId);
            e.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
            e.Property(x => x.Documento).HasMaxLength(20);
            e.Property(x => x.Telefono).HasMaxLength(20).IsRequired();
            e.Property(x => x.Correo).HasMaxLength(150);
            e.Property(x => x.Ciudad).HasMaxLength(100).IsRequired();
            e.Property(x => x.Direccion).HasMaxLength(200);
            e.Property(x => x.NumeroFormulario).HasMaxLength(30);
            e.Property(x => x.Elaboro).HasMaxLength(150);
            e.Property(x => x.FechaAdopcion).HasColumnType("date");
            e.Property(x => x.Observaciones).HasMaxLength(500);
            e.Property(x => x.RegistradoPorId).HasMaxLength(450);
            e.HasOne(x => x.Beneficiario).WithOne(x => x.Adoptante).HasForeignKey<AdoptanteBeneficiario>(x => x.BeneficiarioId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EventoClinico>(e =>
        {
            e.ToTable("EventoClinico");
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Fecha).HasColumnType("date");
            e.Property(x => x.Descripcion).HasMaxLength(1000).IsRequired();
            e.Property(x => x.Responsable).HasMaxLength(150).IsRequired();
            e.Property(x => x.Producto).HasMaxLength(100);
            e.Property(x => x.Laboratorio).HasMaxLength(100);
            e.Property(x => x.PesoKg).HasPrecision(5, 2);
            e.Property(x => x.Resultado).HasMaxLength(100);
            e.Property(x => x.RegistradoPorId).HasMaxLength(450);
            e.HasIndex(x => new { x.BeneficiarioId, x.Fecha });
            e.HasOne(x => x.Beneficiario).WithMany().HasForeignKey(x => x.BeneficiarioId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FotoEventoClinico>(e =>
        {
            e.ToTable("FotoEventoClinico");
            e.Property(x => x.Ruta).HasMaxLength(300).IsRequired();
            e.HasOne(x => x.Evento).WithMany(x => x.Fotos).HasForeignKey(x => x.EventoClinicoId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Apadrinamiento>(e =>
        {
            e.ToTable("Apadrinamiento");
            e.Property(x => x.PadrinoId).HasMaxLength(450).IsRequired();
            e.Property(x => x.ValorMensual).HasPrecision(14, 2);
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.EsalId, x.BeneficiarioId, x.Estado });
            e.HasIndex(x => x.PadrinoId);
            // Un padrino solo puede tener un apadrinamiento activo por beneficiario
            e.HasIndex(x => new { x.PadrinoId, x.BeneficiarioId }).IsUnique().HasFilter("[Estado] = N'Activo'");
            e.HasOne(x => x.Esal).WithMany().HasForeignKey(x => x.EsalId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Beneficiario).WithMany().HasForeignKey(x => x.BeneficiarioId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.PadrinoId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Causa>(e =>
        {
            e.ToTable("Causa");
            e.Property(x => x.Titulo).HasMaxLength(120).IsRequired();
            e.Property(x => x.Descripcion).HasMaxLength(3000).IsRequired();
            e.Property(x => x.Meta).HasPrecision(14, 2);
            e.Property(x => x.FechaLimite).HasColumnType("date");
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.MotivoCierre).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.CreadaPorId).HasMaxLength(450);
            e.Property(x => x.Justificacion).HasMaxLength(1000);
            e.Property(x => x.MotivoRechazo).HasMaxLength(500);
            e.Property(x => x.RevisadaPorId).HasMaxLength(450);
            e.HasIndex(x => new { x.EsalId, x.Estado });
            e.HasOne(x => x.Esal).WithMany().HasForeignKey(x => x.EsalId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<DocumentoTransparencia>().WithMany().HasForeignKey(x => x.RendicionDocumentoId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EvaluacionComportamiento>(e =>
        {
            e.ToTable("EvaluacionComportamiento");
            e.Property(x => x.Momento).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Fecha).HasColumnType("date");
            e.Property(x => x.Conductas).HasMaxLength(200).IsRequired();
            e.Property(x => x.Metodo).HasMaxLength(100).IsRequired();
            e.Property(x => x.Observaciones).HasMaxLength(1000);
            e.Property(x => x.RegistradoPorId).HasMaxLength(450);
            e.HasIndex(x => new { x.BeneficiarioId, x.Fecha });
            e.HasOne(x => x.Beneficiario).WithMany().HasForeignKey(x => x.BeneficiarioId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FotoCausa>(e =>
        {
            e.ToTable("FotoCausa");
            e.Property(x => x.Ruta).HasMaxLength(300).IsRequired();
            e.HasOne(x => x.Causa).WithMany(x => x.Fotos).HasForeignKey(x => x.CausaId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---------------- Sprint 5 ----------------

        // Registro de avisos de Wompi (HU-044). Sin filtro por ESAL: lo escribe un endpoint anónimo
        // y solo se consulta filtrando explícitamente por la fundación.
        builder.Entity<EventoPasarela>(e =>
        {
            e.ToTable("EventoPasarela");
            e.Property(x => x.Evento).HasMaxLength(60);
            e.Property(x => x.TransaccionId).HasMaxLength(60);
            e.Property(x => x.Referencia).HasMaxLength(60);
            e.Property(x => x.EstadoTransaccion).HasMaxLength(20);
            e.Property(x => x.Resultado).HasConversion<string>().HasMaxLength(25);
            e.Property(x => x.Detalle).HasMaxLength(300);
            e.Property(x => x.Cuerpo).IsRequired();
            e.HasIndex(x => new { x.EsalId, x.FechaRecepcion });
            e.HasIndex(x => x.TransaccionId);
            e.HasOne<Esal>().WithMany().HasForeignKey(x => x.EsalId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Donacion>().WithMany().HasForeignKey(x => x.DonacionId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ConfigAdopcion>(e =>
        {
            e.ToTable("ConfigAdopcion");
            e.HasKey(x => x.EsalId);
            e.Property(x => x.Recomendaciones).HasMaxLength(3000).IsRequired();
            e.Property(x => x.ValorAporte).HasPrecision(14, 2).HasDefaultValue(ConfigAdopcion.ValorAportePredeterminado);
            e.Property(x => x.MensajeToxoplasmosis).HasMaxLength(1500);
            e.Property(x => x.ImagenToxoplasmosisRuta).HasMaxLength(300);
            e.HasOne(x => x.Esal).WithOne().HasForeignKey<ConfigAdopcion>(x => x.EsalId);
        });

        builder.Entity<SolicitudAdopcion>(e =>
        {
            e.ToTable("SolicitudAdopcion");
            e.Property(x => x.UsuarioId).HasMaxLength(450).IsRequired();
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
            // Sección 1: datos personales (HU-030)
            e.Property(x => x.NombreCompleto).HasMaxLength(150);
            e.Property(x => x.Cedula).HasMaxLength(10);
            e.Property(x => x.Celular).HasMaxLength(10);
            e.Property(x => x.Ciudad).HasMaxLength(100);
            e.Property(x => x.Direccion).HasMaxLength(200);
            e.Property(x => x.Ocupacion).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.DetalleOcupacion).HasMaxLength(150);
            e.Property(x => x.ReferenciaNombre).HasMaxLength(150);
            e.Property(x => x.ReferenciaCelular).HasMaxLength(10);
            e.Property(x => x.ReferenciaRelacion).HasMaxLength(60);
            // Sección 2: mascotas (HU-031)
            e.Property(x => x.Mascotas).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.OtraMascota).HasMaxLength(100);
            e.Property(x => x.GatoEsterilizacion).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.GatoVacunas).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.CarneVacunasRuta).HasMaxLength(300);
            e.Property(x => x.PerroSociabilidad).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.QuePasoMascota).HasMaxLength(500);
            // Preguntas del formato de El Reino de los Gatos que faltaban
            e.Property(x => x.TelefonoAdicional).HasMaxLength(10);
            e.Property(x => x.Barrio).HasMaxLength(100);
            e.Property(x => x.EdadMascotas).HasMaxLength(100);
            e.Property(x => x.GatoSalud).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.MotivoAdopcion).HasMaxLength(1000);
            e.Property(x => x.GatoBuscado).HasMaxLength(1000);
            e.Property(x => x.LugarParaMascota).HasMaxLength(300);
            e.Property(x => x.NinosEdades).HasConversion<string>().HasMaxLength(25);
            e.Property(x => x.NinosOtro).HasMaxLength(150);
            e.Property(x => x.MejorCualidadGato).HasMaxLength(300);
            e.Property(x => x.CostoMensualEstimado).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Comentarios).HasMaxLength(1000);
            // Sección 3: hogar y compromisos (HU-032)
            e.Property(x => x.TipoVivienda).HasConversion<string>().HasMaxLength(15);
            e.Property(x => x.TenenciaVivienda).HasConversion<string>().HasMaxLength(15);
            e.Property(x => x.Codigo).HasMaxLength(20);
            e.HasIndex(x => x.Codigo).IsUnique(); // EF agrega el filtro "Codigo IS NOT NULL"
            // Revisión de la fundación (HU-033)
            e.Property(x => x.MotivoRechazo).HasMaxLength(300);
            e.Property(x => x.RevisadoPorId).HasMaxLength(450);
            // Cita presencial y resultado (HU-034)
            e.Property(x => x.LugarCita).HasMaxLength(200);
            e.Property(x => x.IndicacionesCita).HasMaxLength(300);
            e.Property(x => x.ObservacionesResultado).HasMaxLength(500);
            e.HasOne(x => x.BeneficiarioAdoptado).WithMany().HasForeignKey(x => x.BeneficiarioAdoptadoId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.EsalId, x.Estado });
            // Una persona solo tiene un borrador por fundación: si vuelve, continúa el mismo
            e.HasIndex(x => new { x.EsalId, x.UsuarioId }).IsUnique().HasFilter("[Estado] = N'Borrador'");
            e.HasOne(x => x.Esal).WithMany().HasForeignKey(x => x.EsalId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---- Sprint 4: tienda (HU-023) ----

        builder.Entity<Producto>(e =>
        {
            e.ToTable("Producto");
            e.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
            e.Property(x => x.Descripcion).HasMaxLength(2000).IsRequired();
            e.Property(x => x.Precio).HasPrecision(14, 2);
            e.Property(x => x.Categoria).HasMaxLength(60);
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.CreadoPorId).HasMaxLength(450);
            e.HasIndex(x => new { x.EsalId, x.Estado });
            e.HasOne(x => x.Esal).WithMany().HasForeignKey(x => x.EsalId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FotoProducto>(e =>
        {
            e.ToTable("FotoProducto");
            e.Property(x => x.Ruta).HasMaxLength(300).IsRequired();
            e.HasOne(x => x.Producto).WithMany(x => x.Fotos).HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Sprint 4: chat de la tienda (HU-025) ----

        builder.Entity<ConversacionTienda>(e =>
        {
            e.ToTable("ConversacionTienda");
            e.Property(x => x.DonanteId).HasMaxLength(450).IsRequired();
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
            // Una sola conversación por donante y producto
            e.HasIndex(x => new { x.DonanteId, x.ProductoId }).IsUnique();
            e.HasIndex(x => new { x.EsalId, x.Estado, x.FechaUltimoMensaje });
            e.HasOne(x => x.Esal).WithMany().HasForeignKey(x => x.EsalId).OnDelete(DeleteBehavior.Restrict);
            // Un producto con conversaciones no se elimina: se oculta
            e.HasOne(x => x.Producto).WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.DonanteId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MensajeTienda>(e =>
        {
            e.ToTable("MensajeTienda");
            e.Property(x => x.AutorId).HasMaxLength(450).IsRequired();
            e.Property(x => x.Texto).HasMaxLength(1000).IsRequired();
            e.HasIndex(x => new { x.ConversacionId, x.Id });
            e.HasOne(x => x.Conversacion).WithMany(x => x.Mensajes).HasForeignKey(x => x.ConversacionId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Sprint 4: pedidos de la tienda (HU-026) ----

        builder.Entity<Pedido>(e =>
        {
            e.ToTable("Pedido");
            e.Property(x => x.Codigo).HasMaxLength(30).IsRequired();
            e.HasIndex(x => x.Codigo).IsUnique();
            e.Property(x => x.DonanteId).HasMaxLength(450).IsRequired();
            e.Property(x => x.PrecioUnitario).HasPrecision(14, 2);
            e.Property(x => x.Total).HasPrecision(14, 2);
            e.Property(x => x.Notas).HasMaxLength(500);
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.RegistradoPorId).HasMaxLength(450);
            e.HasIndex(x => new { x.EsalId, x.Estado });
            e.HasOne(x => x.Conversacion).WithMany().HasForeignKey(x => x.ConversacionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Producto).WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.DonanteId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---- Sprint 4: boletín (HU-027) ----

        builder.Entity<Publicacion>(e =>
        {
            e.ToTable("Publicacion");
            e.Property(x => x.Titulo).HasMaxLength(150).IsRequired();
            e.Property(x => x.Resumen).HasMaxLength(300);
            e.Property(x => x.Contenido).HasMaxLength(8000).IsRequired();
            e.Property(x => x.Categoria).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ImagenRuta).HasMaxLength(300);
            e.Property(x => x.LugarEvento).HasMaxLength(200);
            e.Property(x => x.CreadaPorId).HasMaxLength(450);
            e.HasIndex(x => new { x.EsalId, x.Estado, x.FechaPublicacion });
            e.HasIndex(x => new { x.Estado, x.Categoria, x.FechaEvento });
            e.HasOne(x => x.Esal).WithMany().HasForeignKey(x => x.EsalId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Causa).WithMany().HasForeignKey(x => x.CausaId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------------- Sprint 6 ----------------

        builder.Entity<Medicamento>(e =>
        {
            e.ToTable("Medicamento");
            e.Property(x => x.NombreComercial).HasMaxLength(150).IsRequired();
            e.Property(x => x.PrincipioActivo).HasMaxLength(150).IsRequired();
            e.Property(x => x.FechaVencimiento).HasColumnType("date");
            e.Property(x => x.Uso).HasMaxLength(300).IsRequired();
            e.Property(x => x.Via).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Dosis).HasMaxLength(150).IsRequired();
            e.Property(x => x.Presentacion).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Cantidad).HasPrecision(10, 2);
            e.Property(x => x.CantidadMinima).HasPrecision(10, 2);
            e.Property(x => x.RegistradoPorId).HasMaxLength(450);
            e.Property(x => x.VencimientoAvisado).HasColumnType("date");
            e.HasIndex(x => new { x.EsalId, x.FechaVencimiento });
            e.HasOne(x => x.Esal).WithMany().HasForeignKey(x => x.EsalId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MovimientoMedicamento>(e =>
        {
            e.ToTable("MovimientoMedicamento");
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(15);
            e.Property(x => x.Cantidad).HasPrecision(10, 2);
            e.Property(x => x.CantidadResultante).HasPrecision(10, 2);
            e.Property(x => x.Nota).HasMaxLength(300);
            e.Property(x => x.RegistradoPorId).HasMaxLength(450);
            e.HasIndex(x => new { x.MedicamentoId, x.Fecha });
            e.HasOne(x => x.Medicamento).WithMany(x => x.Movimientos).HasForeignKey(x => x.MedicamentoId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Beneficiario).WithMany().HasForeignKey(x => x.BeneficiarioId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EventoAgenda>(e =>
        {
            e.ToTable("EventoAgenda");
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(15);
            e.Property(x => x.Descripcion).HasMaxLength(300).IsRequired();
            e.Property(x => x.Dosis).HasMaxLength(150);
            e.Property(x => x.MotivoCancelacion).HasMaxLength(300);
            e.Property(x => x.CreadoPorId).HasMaxLength(450);
            e.Property(x => x.RealizadoPorId).HasMaxLength(450);
            e.Property(x => x.CanceladoPorId).HasMaxLength(450);
            // Agenda general por fecha y agenda de cada beneficiario
            e.HasIndex(x => new { x.EsalId, x.Estado, x.FechaProgramada });
            e.HasIndex(x => new { x.BeneficiarioId, x.FechaProgramada });
            e.HasIndex(x => x.SerieId);
            e.HasIndex(x => x.EventoClinicoId).IsUnique().HasFilter("[EventoClinicoId] IS NOT NULL");
            e.HasOne(x => x.Beneficiario).WithMany().HasForeignKey(x => x.BeneficiarioId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Medicamento).WithMany().HasForeignKey(x => x.MedicamentoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.EventoClinico).WithMany().HasForeignKey(x => x.EventoClinicoId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ConfigSalud>(e =>
        {
            e.ToTable("ConfigSalud");
            e.HasKey(x => x.EsalId);
            e.HasOne<Esal>().WithOne().HasForeignKey<ConfigSalud>(x => x.EsalId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<NovedadBeneficiario>(e =>
        {
            e.ToTable("NovedadBeneficiario");
            e.Property(x => x.Texto).HasMaxLength(2000).IsRequired();
            e.Property(x => x.PublicadaPorId).HasMaxLength(450);
            e.HasIndex(x => new { x.BeneficiarioId, x.Fecha });
            e.HasOne(x => x.Beneficiario).WithMany().HasForeignKey(x => x.BeneficiarioId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FotoNovedad>(e =>
        {
            e.ToTable("FotoNovedad");
            e.Property(x => x.Ruta).HasMaxLength(300).IsRequired();
            e.HasOne(x => x.Novedad).WithMany(x => x.Fotos).HasForeignKey(x => x.NovedadId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RendicionCausa>(e =>
        {
            e.ToTable("RendicionCausa");
            e.Property(x => x.Texto).HasMaxLength(4000).IsRequired();
            e.Property(x => x.ValorEjecutado).HasPrecision(14, 2);
            e.Property(x => x.Recaudado).HasPrecision(14, 2);
            e.Property(x => x.PublicadaPorId).HasMaxLength(450);
            e.HasIndex(x => x.CausaId).IsUnique(); // una sola rendición por causa
            e.HasOne(x => x.Causa).WithMany().HasForeignKey(x => x.CausaId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<DocumentoTransparencia>().WithMany().HasForeignKey(x => x.DocumentoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Publicacion>().WithMany().HasForeignKey(x => x.PublicacionId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ArchivoRendicion>(e =>
        {
            e.ToTable("ArchivoRendicion");
            e.Property(x => x.Ruta).HasMaxLength(300).IsRequired();
            e.Property(x => x.NombreOriginal).HasMaxLength(200).IsRequired();
            e.HasOne(x => x.Rendicion).WithMany(x => x.Archivos).HasForeignKey(x => x.RendicionId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RevisionDiaria>(e =>
        {
            e.ToTable("RevisionDiaria");
            e.HasKey(x => new { x.Fecha, x.Tarea });
            e.Property(x => x.Fecha).HasColumnType("date");
            e.Property(x => x.Tarea).HasMaxLength(60);
            e.Property(x => x.Resumen).HasMaxLength(500);
        });

        // Trazabilidad: sin filtro por fundación (lo consultan el veedor y el superadministrador)
        builder.Entity<RegistroAuditoria>(e =>
        {
            e.ToTable("RegistroAuditoria");
            e.Property(x => x.UsuarioId).HasMaxLength(450);
            e.Property(x => x.UsuarioNombre).HasMaxLength(150).IsRequired();
            e.Property(x => x.Entidad).HasMaxLength(80).IsRequired();
            e.Property(x => x.EntidadId).HasMaxLength(100).IsRequired();
            e.Property(x => x.Accion).HasConversion<string>().HasMaxLength(15);
            e.Property(x => x.Cambios).IsRequired();
            e.HasIndex(x => x.Fecha);
            e.HasIndex(x => new { x.EsalId, x.Fecha });
            e.HasIndex(x => new { x.Entidad, x.EntidadId });
        });

        // ---- Libro de fondos ----
        builder.Entity<Fondo>(e =>
        {
            e.ToTable("Fondo");
            e.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(15);
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.Descripcion).HasMaxLength(500);
            e.Property(x => x.CreadoPorId).HasMaxLength(450);
            e.HasIndex(x => x.CausaId).IsUnique(); // una causa, un fondo (EF agrega "CausaId IS NOT NULL")
            // Un solo fondo general y uno de apadrinamientos por fundación
            e.HasIndex(x => new { x.EsalId, x.Tipo }).IsUnique().HasFilter("[Tipo] IN (N'General', N'Apadrinamiento')");
            e.HasOne(x => x.Esal).WithMany().HasForeignKey(x => x.EsalId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Causa).WithMany().HasForeignKey(x => x.CausaId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MovimientoFondo>(e =>
        {
            e.ToTable("MovimientoFondo");
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Valor).HasPrecision(14, 2);
            e.Property(x => x.Concepto).HasMaxLength(300).IsRequired();
            e.Property(x => x.Proveedor).HasMaxLength(150);
            e.Property(x => x.SoporteRuta).HasMaxLength(300);
            e.Property(x => x.RegistradoPorId).HasMaxLength(450);
            e.Property(x => x.MotivoAnulacion).HasMaxLength(300);
            e.Property(x => x.AnuladoPorId).HasMaxLength(450);
            e.HasIndex(x => new { x.FondoId, x.Fecha });
            e.HasIndex(x => new { x.EsalId, x.Fecha });
            e.HasOne(x => x.Fondo).WithMany(x => x.Movimientos).HasForeignKey(x => x.FondoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.FondoRelacionado).WithMany().HasForeignKey(x => x.FondoRelacionadoId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SalidaEspecie>(e =>
        {
            e.ToTable("SalidaEspecie");
            e.Property(x => x.Uso).HasMaxLength(300).IsRequired();
            e.Property(x => x.FotoRuta).HasMaxLength(300);
            e.Property(x => x.RegistradoPorId).HasMaxLength(450);
            e.Property(x => x.MotivoAnulacion).HasMaxLength(300);
            e.Property(x => x.AnuladoPorId).HasMaxLength(450);
            e.HasIndex(x => new { x.DonacionEspecieId, x.Fecha });
            e.HasOne(x => x.DonacionEspecie).WithMany(x => x.Salidas).HasForeignKey(x => x.DonacionEspecieId).OnDelete(DeleteBehavior.Restrict);
        });

        // Filtro por ESAL en cada entidad que pertenece a una fundación.
        // Al crear una entidad nueva con EsalId, agréguenla aquí.
        AplicarFiltroEsal<EsalModulo>(builder);
        AplicarFiltroEsal<RedSocial>(builder);
        AplicarFiltroEsal<ConfigPasarela>(builder);
        AplicarFiltroEsal<FotoEsal>(builder);
        AplicarFiltroEsal<DocumentoTransparencia>(builder);
        AplicarFiltroEsal<DatosDonacion>(builder);
        AplicarFiltroEsal<Donacion>(builder);
        AplicarFiltroEsal<NecesidadEspecie>(builder);
        AplicarFiltroEsal<DonacionEspecie>(builder);
        AplicarFiltroEsal<PostulacionVoluntario>(builder);
        AplicarFiltroEsal<Beneficiario>(builder);
        AplicarFiltroEsal<HistorialEstadoBeneficiario>(builder);
        AplicarFiltroEsal<AdoptanteBeneficiario>(builder);
        AplicarFiltroEsal<EventoClinico>(builder);
        AplicarFiltroEsal<FotoEventoClinico>(builder);
        AplicarFiltroEsal<Apadrinamiento>(builder);
        AplicarFiltroEsal<Causa>(builder);
        AplicarFiltroEsal<FotoCausa>(builder);
        AplicarFiltroEsal<EvaluacionComportamiento>(builder);
        AplicarFiltroEsal<ConfigAdopcion>(builder);
        AplicarFiltroEsal<SolicitudAdopcion>(builder);
        AplicarFiltroEsal<Producto>(builder);
        AplicarFiltroEsal<FotoProducto>(builder);
        AplicarFiltroEsal<ConversacionTienda>(builder);
        AplicarFiltroEsal<MensajeTienda>(builder);
        AplicarFiltroEsal<Pedido>(builder);
        AplicarFiltroEsal<Publicacion>(builder);
        AplicarFiltroEsal<Medicamento>(builder);
        AplicarFiltroEsal<MovimientoMedicamento>(builder);
        AplicarFiltroEsal<EventoAgenda>(builder);
        AplicarFiltroEsal<ConfigSalud>(builder);
        AplicarFiltroEsal<NovedadBeneficiario>(builder);
        AplicarFiltroEsal<FotoNovedad>(builder);
        AplicarFiltroEsal<RendicionCausa>(builder);
        AplicarFiltroEsal<ArchivoRendicion>(builder);
        AplicarFiltroEsal<Fondo>(builder);
        AplicarFiltroEsal<MovimientoFondo>(builder);
        AplicarFiltroEsal<SalidaEspecie>(builder);
    }

    private void AplicarFiltroEsal<T>(ModelBuilder builder) where T : class, IPerteneceAEsal
    {
        builder.Entity<T>().HasQueryFilter(e => SinFiltroEsal || e.EsalId == EsalIdActual);
    }
}
