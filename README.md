# MunerApp

Plataforma web multi-entidad para la gestión transparente de donaciones y formas de apoyo a ESALes.
Piloto: fundación El Reino de los Gatos. Proyecto de Gestión del Conocimiento · Universidad de Cundinamarca.

**Stack:** ASP.NET Core MVC (.NET 8) · EF Core 8 · ASP.NET Identity · Azure SQL · Azure App Service · Google OAuth · Wompi

## Estructura

```
MunerApp.sln
└── src/
    ├── MunerApp.Domain/          Entidades, constantes (roles, perfiles, módulos), enums
    ├── MunerApp.Application/     Interfaces de servicios (IEsalActual, IModuloService, ICorreoService)
    ├── MunerApp.Infrastructure/  DbContext, Identity, seed, servicios (módulos, correo)
    └── MunerApp.Web/             Controladores, vistas, filtros, Program.cs
```

## Primer arranque (cada integrante)

Requisitos: .NET 8 SDK, SQL Server LocalDB (viene con Visual Studio) y la herramienta de EF:

```bash
dotnet tool install --global dotnet-ef
```

1. Clonar y compilar:

   ```bash
   git clone <url-del-repo>
   cd MunerApp
   git checkout develop
   dotnet build
   ```

2. Configurar los secretos locales (nunca van en `appsettings.json`):

   ```bash
   cd src/MunerApp.Web
   dotnet user-secrets set "Seed:SuperAdmin:Email" "superadmin@munerapp.local"
   dotnet user-secrets set "Seed:SuperAdmin:Password" "Cambiar123"
   # Cuando estén listos (HU-003 y HU-004):
   dotnet user-secrets set "Autenticacion:Google:ClientId" "<client-id>"
   dotnet user-secrets set "Autenticacion:Google:ClientSecret" "<client-secret>"
   dotnet user-secrets set "Correo:Usuario" "<correo-gmail>"
   dotnet user-secrets set "Correo:Contrasena" "<contraseña-de-aplicación>"
   ```

3. Crear la base de datos. **La primera migración la crea solo Esteban (HU-006)** y la sube al repo; los demás solo ejecutan `database update`:

   ```bash
   # Solo Esteban, una vez:
   dotnet ef migrations add HU006_Inicial -p src/MunerApp.Infrastructure -s src/MunerApp.Web -o Persistence/Migrations

   # Todos:
   dotnet ef database update -p src/MunerApp.Infrastructure -s src/MunerApp.Web
   ```

4. Ejecutar:

   ```bash
   dotnet run --project src/MunerApp.Web
   ```

   Al arrancar se crean los 4 roles y el superadministrador configurado en los secretos.

## Sprint 1: estado del código

| HU | Historia | Dónde está |
|---|---|---|
| 001 | Registro de donantes | `Controllers/CuentaController.cs` → `Registrar` |
| 002 | Inicio de sesión y redirección por rol | `CuentaController` → `IniciarSesion`, `RedirigirSegunRolAsync` |
| 003 | Login con Gmail | `CuentaController` → `LoginExterno`, `LoginExternoCallback` · `Views/Cuenta/_BotonGoogle.cshtml` |
| 004 | Recuperar contraseña | `CuentaController` → `OlvideContrasena`, `RestablecerContrasena` · `Servicios/InvitacionService.cs` |
| 005 | Cierre y expiración de sesión | `CuentaController.CerrarSesion` · `Program.cs` (cookie de 20 min) |
| 006 | Registro de ESAL | `Areas/Plataforma/Controllers/EsalesController.cs` |
| 007 | Módulos configurables | `EsalesController.Modulos` · `Filtros/RequiereModuloAttribute.cs` · `Areas/Fundacion/.../PanelController.cs` |
| 008 | Usuarios y roles de la ESAL | `Areas/Fundacion/Controllers/UsuariosController.cs` · filtro global en `MunerAppDbContext` |
| 011 | Redes sociales | `Areas/Fundacion/Controllers/RedesController.cs` |
| 045 | Llaves de Wompi | `Areas/Fundacion/Controllers/PasarelaController.cs` · `Infrastructure/Servicios/WompiService.cs`, `SecretosService.cs` |

Cada responsable revisa su historia, prueba sus 3 escenarios y la sube por pull request.

### Rutas principales

| Ruta | Quién | Qué hace |
|---|---|---|
| `/Plataforma/Esales` | Superadministrador | Registrar fundaciones, editarlas, activarlas y elegir módulos |
| `/Fundacion/Panel` | Administrador o voluntario de ESAL | Inicio del panel con los módulos activos |
| `/Fundacion/Usuarios` | Administrador de ESAL | Equipo de la fundación (crear y editar solo el perfil Principal) |
| `/Fundacion/Redes` | Administrador principal | Redes sociales |
| `/Fundacion/Pasarela` | Administrador principal | Llaves de Wompi |
| `/Home/Estilos` | Solo en desarrollo | Guía de estilos |

### Probar sin correo configurado

Si el SMTP no está configurado, en **desarrollo** la plataforma muestra en pantalla el enlace para crear o restablecer la contraseña, para que puedan probar las invitaciones y la recuperación sin enviar correos.

### Probar el login con Google

1. En Google Cloud Console crear un proyecto → **APIs y servicios → Pantalla de consentimiento OAuth** (tipo Externo) → **Credenciales → Crear ID de cliente de OAuth** (aplicación web).
2. URI de redireccionamiento autorizado: `https://localhost:7180/signin-google` (y luego la de Azure: `https://<app>.azurewebsites.net/signin-google`).
3. Guardar el Client ID y el Client Secret con `dotnet user-secrets` (ver arriba).

### Probar Wompi (sandbox)

Crear una cuenta de pruebas en Wompi, entrar como administrador principal a `/Fundacion/Pasarela` y pegar la llave pública (`pub_test_...`), el secreto de integridad (`test_integrity_...`) y el de eventos (`test_events_...`).

## Sprint 2: estado del código

| HU | Historia | Dónde está |
|---|---|---|
| 009 | Perfil institucional | `Areas/Fundacion/Controllers/PerfilController.cs` |
| 010 | Directorio y perfil público | `Controllers/FundacionesController.cs` → `Index`, `Perfil` (`/fundaciones`, `/fundaciones/{slug}`) |
| 012 | Documentos de transparencia | `Areas/Fundacion/Controllers/DocumentosController.cs` |
| 013 | Datos oficiales para donar | `Areas/Fundacion/Controllers/DatosDonacionController.cs` · `FundacionesController.Donar` |
| 014 | Reporte de donación y "Mis donaciones" | `Controllers/DonacionesController.cs` (`/fundaciones/{slug}/reportar-donacion`, `/mis-donaciones`) |
| 015 | Confirmar o rechazar donaciones, comprobante PDF y notificaciones | `Areas/Fundacion/Controllers/DonacionesController.cs` · `Infrastructure/Servicios/ComprobantePdfService.cs`, `NotificacionService.cs` |
| 016 | Otras formas de ayudar | `FundacionesController.ConstruirFormasAyuda` · sección `#como-ayudar` del perfil |
| 035 | Postulación de voluntarios | `Controllers/PostulacionesController.cs` · consulta en `Areas/Fundacion/Controllers/PostulacionesController.cs` |

También completa el escenario 3 de HU-006 (una fundación inactiva no aparece en `/fundaciones`) y el escenario 1 de HU-011 (redes en el perfil).

### Migración del Sprint 2 (una sola persona)

```bash
dotnet ef migrations add Sprint2_PerfilDonaciones -p src/MunerApp.Infrastructure -s src/MunerApp.Web -o Persistence/Migrations
dotnet ef database update -p src/MunerApp.Infrastructure -s src/MunerApp.Web
```

Al arrancar, las fundaciones que ya existían reciben su dirección pública (`slug`) automáticamente.

### Archivos que suben los usuarios

- Se guardan con `IAlmacenamientoArchivos` (`Infrastructure/Servicios/AlmacenamientoLocal.cs`). En desarrollo quedan en `src/MunerApp.Web/App_Data/archivos` (ignorada por Git).
- Públicos (logo, galería, documentos): se sirven en `/archivos/...`. Privados (soportes de pago y académicos): solo se descargan por controladores que validan quién los pide.
- Se valida la extensión, el tamaño y la firma real del archivo (`Validacion/ValidadorArchivos.cs`).
- En Azure App Service configurar `Archivos:RutaBase` = `/home/data/archivos` (Linux) o `D:\home\data\archivos` (Windows), que es almacenamiento persistente.

### Páginas públicas y el filtro por ESAL

Las páginas públicas (`/fundaciones/...`), "Mis donaciones" y "Mis postulaciones" consultan con `IgnoreQueryFilters()` y **siempre** filtran explícitamente por la fundación consultada o por el usuario autenticado. Así un administrador de una fundación también ve completo el perfil de otra, y un donante ve sus donaciones a varias fundaciones.

## Sprint 5: estado del código

| HU | Historia | Dónde está |
|---|---|---|
| 029 | Recomendaciones y responsabilidades antes de adoptar | `Controllers/AdopcionesController.cs` → `Recomendaciones` (`/fundaciones/{slug}/adoptar`) · configuración de la fundación en `Areas/Fundacion/Controllers/AdopcionesController.cs` |

### Solicitud de adopción

- La solicitud (`SolicitudAdopcion`) nace en estado **Borrador** cuando la persona acepta las recomendaciones. Una persona solo tiene un borrador por fundación: si vuelve, continúa el mismo.
- El formulario (`/fundaciones/{slug}/adoptar/formulario`) solo se abre si existe ese borrador, así no se puede entrar por URL sin aceptar.
- Las recomendaciones se leen sin cuenta; para aceptarlas se pide iniciar sesión. Las cuentas de una fundación no pueden solicitar adopciones.
- Cada fundación edita sus recomendaciones en **Panel → Adopción** (una por línea). Mientras no las guarde, se muestran las predeterminadas (`ConfigAdopcion.RecomendacionesPredeterminadas`).
- Requiere el módulo **Adopción** activo en la fundación.

## Cómo se protege un módulo

```csharp
[Authorize(Roles = Roles.AdministradorESAL)]
[RequiereModulo(CodigosModulo.Tienda)]
public class ProductosController : Controller { }
```

## Filtro por ESAL

Toda entidad nueva que pertenezca a una fundación debe:

1. Implementar `IPerteneceAEsal` (propiedad `EsalId`).
2. Registrarse en `MunerAppDbContext.OnModelCreating` con `AplicarFiltroEsal<Entidad>(builder);`.

Los usuarios de una ESAL (administradores y voluntarios) solo ven los datos de su fundación. Las páginas públicas filtran explícitamente por la ESAL que se consulta. El superadministrador puede usar `IgnoreQueryFilters()` en el panel de plataforma; las páginas públicas también, filtrando siempre de forma explícita (ver Sprint 2).

## Flujo de trabajo en Git

```bash
git checkout develop
git pull
git checkout -b feature/HU-004-recuperar-contrasena
# ...trabajar...
git commit -m "HU-004: agrega envío del enlace de recuperación"
git push -u origin feature/HU-004-recuperar-contrasena
# Abrir un pull request hacia develop y pedir revisión a otro integrante
```

- `main`: producción (solo se actualiza al cerrar el sprint).
- `develop`: integración.
- Nadie hace push directo a `main` ni a `develop`.
