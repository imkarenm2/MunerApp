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

    // Sprint 5
    public DbSet<ConfigAdopcion> ConfigAdopciones => Set<ConfigAdopcion>();
    public DbSet<SolicitudAdopcion> SolicitudesAdopcion => Set<SolicitudAdopcion>();
    public DbSet<EventoPasarela> EventosPasarela => Set<EventoPasarela>();

    // Sprint 6
    public DbSet<Medicamento> Medicamentos => Set<Medicamento>();
    public DbSet<MovimientoMedicamento> MovimientosMedicamento => Set<MovimientoMedicamento>();
    public DbSet<EventoAgenda> EventosAgenda => Set<EventoAgenda>();

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
            e.Property(x => x.Documento).HasMaxLength(20).IsRequired();
            e.Property(x => x.Telefono).HasMaxLength(20).IsRequired();
            e.Property(x => x.Correo).HasMaxLength(150);
            e.Property(x => x.Ciudad).HasMaxLength(100).IsRequired();
            e.Property(x => x.Direccion).HasMaxLength(200).IsRequired();
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
            e.Property(x => x.CreadaPorId).HasMaxLength(450);
            e.HasIndex(x => new { x.EsalId, x.Estado });
            e.HasOne(x => x.Esal).WithMany().HasForeignKey(x => x.EsalId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<DocumentoTransparencia>().WithMany().HasForeignKey(x => x.RendicionDocumentoId).OnDelete(DeleteBehavior.Restrict);
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

        // Filtro por ESAL en cada entidad que pertenece a una fundación.
        // Al crear una entidad nueva con EsalId, agréguenla aquí.
        AplicarFiltroEsal<EsalModulo>(builder);
        AplicarFiltroEsal<RedSocial>(builder);
        AplicarFiltroEsal<ConfigPasarela>(builder);
        AplicarFiltroEsal<FotoEsal>(builder);
        AplicarFiltroEsal<DocumentoTransparencia>(builder);
        AplicarFiltroEsal<DatosDonacion>(builder);
        AplicarFiltroEsal<Donacion>(builder);
        AplicarFiltroEsal<PostulacionVoluntario>(builder);
        AplicarFiltroEsal<Beneficiario>(builder);
        AplicarFiltroEsal<HistorialEstadoBeneficiario>(builder);
        AplicarFiltroEsal<AdoptanteBeneficiario>(builder);
        AplicarFiltroEsal<EventoClinico>(builder);
        AplicarFiltroEsal<FotoEventoClinico>(builder);
        AplicarFiltroEsal<Apadrinamiento>(builder);
        AplicarFiltroEsal<Causa>(builder);
        AplicarFiltroEsal<FotoCausa>(builder);
        AplicarFiltroEsal<ConfigAdopcion>(builder);
        AplicarFiltroEsal<SolicitudAdopcion>(builder);
        AplicarFiltroEsal<Medicamento>(builder);
        AplicarFiltroEsal<MovimientoMedicamento>(builder);
        AplicarFiltroEsal<EventoAgenda>(builder);
    }

    private void AplicarFiltroEsal<T>(ModelBuilder builder) where T : class, IPerteneceAEsal
    {
        builder.Entity<T>().HasQueryFilter(e => SinFiltroEsal || e.EsalId == EsalIdActual);
    }
}
