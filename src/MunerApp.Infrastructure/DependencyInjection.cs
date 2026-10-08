using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MunerApp.Application.Interfaces;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Infrastructure.Servicios;

namespace MunerApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<MunerAppDbContext>(options =>
            options.UseSqlServer(config.GetConnectionString("DefaultConnection")));

        services.AddScoped<IModuloService, ModuloService>();
        services.AddScoped<ICorreoService, CorreoSmtpService>();
        services.AddScoped<ISecretosService, SecretosService>();
        services.AddHttpClient<IWompiService, WompiService>();

        // Sprint 2
        services.AddSingleton<IAlmacenamientoArchivos, AlmacenamientoLocal>();
        services.AddScoped<INotificacionService, NotificacionService>();
        services.AddSingleton<IComprobanteService, ComprobantePdfService>();
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        // Sprint 6
        services.AddSingleton<IReportesService, ReportesPdfService>();

        return services;
    }
}
