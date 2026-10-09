using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using MunerApp.Application.Interfaces;
using MunerApp.Application.Seguridad;
using MunerApp.Domain.Constantes;
using MunerApp.Infrastructure;
using MunerApp.Infrastructure.Identity;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Infrastructure.Servicios;
using MunerApp.Web.Seguridad;
using MunerApp.Web.Servicios;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IEsalActual, EsalActual>();
builder.Services.AddScoped<InvitacionService>();
builder.Services.AddScoped<CausasPublicas>();
builder.Services.AddScoped<EstadosBeneficiario>();
builder.Services.AddScoped<InventarioMedicamentos>();
builder.Services.AddScoped<ConfirmacionPagosWompi>();
builder.Services.AddInfrastructure(builder.Configuration);

// ---- Identity (documento de diseño, sección 9) ----
builder.Services.AddIdentity<Usuario, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<MunerAppDbContext>()
    .AddDefaultTokenProviders()
    .AddErrorDescriber<IdentityErrorDescriberEs>()
    .AddClaimsPrincipalFactory<MunerAppClaimsFactory>();

// Enlaces de recuperación e invitación: vigencia de 1 hora (HU-004)
builder.Services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(1));

// Si a un usuario le cambian el rol o lo desactivan, su sesión se revalida en máximo 1 minuto (HU-008)
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));

// Sesión: expiración por inactividad (HU-005)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Cuenta/IniciarSesion";
    options.LogoutPath = "/Cuenta/CerrarSesion";
    options.AccessDeniedPath = "/Cuenta/AccesoDenegado";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(builder.Configuration.GetValue("Sesion:MinutosInactividad", 20));
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Politicas.AdminEsalPrincipal, p => p
        .RequireRole(Roles.AdministradorESAL)
        .RequireClaim(MunerAppClaims.Perfil, Perfiles.Principal));

    options.AddPolicy(Politicas.AccesoClinico, p => p.RequireAuthenticatedUser().RequireAssertion(ctx => Politicas.TieneAccesoClinico(ctx.User)));
});

// Login con Gmail (HU-003): se activa cuando se configuran las credenciales de Google
var googleClientId = builder.Configuration["Autenticacion:Google:ClientId"];
if (!string.IsNullOrWhiteSpace(googleClientId))
{
    builder.Services.AddAuthentication().AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = builder.Configuration["Autenticacion:Google:ClientSecret"] ?? string.Empty;
    });
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Formatos de Colombia: $ 50.000 y fechas en español
var culturaCo = new CultureInfo("es-CO");
CultureInfo.DefaultThreadCurrentCulture = culturaCo;
CultureInfo.DefaultThreadCurrentUICulture = culturaCo;
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(culturaCo),
    SupportedCultures = new[] { culturaCo },
    SupportedUICultures = new[] { culturaCo }
});

app.UseHttpsRedirection();
app.UseStaticFiles();

// Archivos públicos que suben las fundaciones (logo, fotos, documentos de transparencia).
// Los soportes privados NO se sirven aquí: se entregan por controladores que validan permisos.
var rutaPublica = AlmacenamientoLocal.RutaPublica(app.Configuration, app.Environment);
Directory.CreateDirectory(rutaPublica);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(rutaPublica),
    RequestPath = AlmacenamientoLocal.RutaUrlPublica,
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        ctx.Context.Response.Headers.CacheControl = "public,max-age=604800";
    }
});
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Panel}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

await DataSeeder.SembrarAsync(app.Services, app.Configuration);

app.Run();
