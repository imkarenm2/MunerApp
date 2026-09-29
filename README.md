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

## Qué ya trae esta base

| Pieza | Dónde | Historia |
|---|---|---|
| Solución en 4 capas y configuración de Identity | toda la solución, `Program.cs` | HU-001 |
| Registro de donantes (con mensajes en español) | `CuentaController.Registrar` | HU-001 |
| Inicio de sesión, bloqueo por intentos, cuenta inactiva | `CuentaController.IniciarSesion` | HU-002 |
| Cierre de sesión y expiración por inactividad (20 min) | `CuentaController.CerrarSesion`, `Program.cs` | HU-005 |
| Modelo de datos del Sprint 1 y catálogo de 8 módulos | `MunerAppDbContext` | HU-006 |
| Filtro global por ESAL | `MunerAppDbContext.AplicarFiltroEsal` | HU-008 |
| Atributo `[RequiereModulo]` | `Web/Filtros/RequiereModuloAttribute.cs` | HU-007 |
| Claims de ESAL, perfil y nombre en la sesión | `MunerAppClaimsFactory` | HU-002 / HU-008 |
| Servicio de correo SMTP | `CorreoSmtpService` | HU-004 |
| Entidades `RedSocial` y `ConfigPasarela` | `Domain/Entities` | HU-011 / HU-045 |

## Pendientes del Sprint 1 (buscar `TODO HU-` en el código)

| Integrante | Historia | Qué falta |
|---|---|---|
| Karen | HU-001 | Revisar los 3 escenarios y ajustar la vista |
| Karen | HU-008 | CRUD de usuarios de la ESAL, asignación de rol y perfil, pruebas de aislamiento |
| Esteban | HU-006 | Primera migración, despliegue en Azure, CRUD de ESAL para el superadministrador |
| Esteban | HU-045 | Formulario de llaves de Wompi, cifrado con Data Protection, validación de la llave pública |
| Jailer | HU-002 | Redirección al panel según el rol |
| Jailer | HU-007 | Pantalla para activar y desactivar módulos, ocultar el menú según los módulos |
| Jailer | HU-005 | Probar la expiración y las rutas protegidas |
| Santiago | HU-003 | `LoginExterno` y `LoginExternoCallback` con Google |
| Santiago | HU-004 | `OlvideContrasena` y `RestablecerContrasena` |
| Santiago | HU-011 | CRUD de redes sociales con validación de URL |

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

Los usuarios de una ESAL (administradores y voluntarios) solo ven los datos de su fundación. Las páginas públicas filtran explícitamente por la ESAL que se consulta. El superadministrador puede usar `IgnoreQueryFilters()` solo en el panel de plataforma.

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
