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
4. Mientras no estén configurados, en desarrollo el botón "Continuar con Google" sale deshabilitado con un aviso; en producción no aparece.

### Confirmación del correo

Las cuentas de donante se activan solo después de confirmar el correo (`SignIn.RequireConfirmedEmail`). Al registrarse se envía un enlace que vence en 1 hora; si vence, al intentar entrar se ofrece enviar otro. Restablecer la contraseña también confirma el correo.
Las cuentas que crean el superadministrador o una fundación ya quedan confirmadas, porque la persona crea su contraseña desde el enlace que le llega al correo.
Si alguien entra con Google con el correo de una cuenta sin confirmar, se confirma con Google y se borra la contraseña anterior, por si la cuenta la creó otra persona con ese correo.

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

## Roles y permisos

| Rol | Quién es |
|---|---|
| Visitante | Persona sin sesión |
| Donante | Cuenta personal; se crea al registrarse o al entrar con Google |
| Voluntario general | Del equipo de una fundación, sin formación en salud |
| Voluntario de salud | Practicante veterinario de la fundación |
| Administrador de consulta | Del equipo de la fundación: consulta y aprueba, no configura |
| Administrador principal | Responsable de la fundación en la plataforma |
| Superadministrador | Equipo de MunerApp |

| Acción | Visitante | Donante | Vol. general | Vol. salud | Admin consulta | Admin principal | Superadmin |
|---|---|---|---|---|---|---|---|
| Ver fundaciones, perfiles, causas y apadrinables | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Donar, reportar aportes y apadrinar | Pide cuenta | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| Postularse como voluntario | Pide cuenta | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| Panel de la fundación | ❌ | ❌ | ✅ | ✅ | ✅ | ✅ | ❌ |
| Ver listado y hoja de vida de beneficiarios | ❌ | ❌ | ✅ | ✅ | ✅ | ✅ | ❌ |
| Registrar, editar y cambiar estado de beneficiarios | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ |
| Ver datos del adoptante y padrinos | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ | ❌ |
| Ver historia clínica | ❌ | ❌ | ❌ | ✅ | ✅ | ✅ | ❌ |
| Registrar eventos clínicos | ❌ | ❌ | ❌ | ✅ | ❌ | ✅ | ❌ |
| Confirmar o rechazar donaciones | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ | ❌ |
| Ver causas y postulaciones de la fundación | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ | ❌ |
| Crear, editar y pausar causas | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ |
| Perfil, transparencia, datos para donar, redes, Wompi | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ |
| Ver el equipo | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ | ❌ |
| Crear, editar y desactivar usuarios del equipo | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ |
| Registrar fundaciones, activarlas y elegir módulos | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |

Reglas adicionales:

- Nadie confirma ni rechaza una donación que reportó él mismo.
- Las cuentas de fundación y el superadministrador no donan, no apadrinan y no se postulan: así una fundación no se puede reportar y confirmar donaciones a sí misma. Para apoyar a título personal se usa otra cuenta.
- Cada usuario de una fundación solo ve datos de su fundación (filtro global por ESAL).
- Si se desactiva un usuario, se le cambia el rol o se desactiva su fundación, su sesión se cierra en máximo 1 minuto.
- Los permisos están en `src/MunerApp.Web/Seguridad/Politicas.cs`. Antes de crear una pantalla nueva, revisen esta tabla y protejan la acción con `[Authorize(...)]`.

## Reglas de los flujos

- **Directorio:** una fundación aparece en `/fundaciones` y en el inicio solo si está activa y tiene descripción corta y misión. Mientras tanto su página se abre con el enlace y el panel le avisa qué le falta.
- **Aprobación de causas:** la fundación propone la causa con una justificación y queda "Por aprobar". El superadministrador la revisa en *Administración → Causas*: si la aprueba se publica; si la devuelve, deja un motivo y la fundación la corrige (vuelve a revisión). Mientras no esté aprobada no se ve en las páginas públicas ni recibe donaciones. El superadministrador también puede crear una causa para una fundación, que se publica de una vez.
- **Causas:** para proponer una causa la fundación debe tener configurados los datos para donar. El botón "Donar a esta causa" lleva el número de la causa hasta el reporte (`?causa=`); cuando la fundación confirma la donación, suma a la barra. Una causa pausada o cerrada no recibe reportes.
- **Apadrinar:** para ofrecer un beneficiario se necesitan los datos para donar. Si el beneficiario es adoptado o fallece, sus apadrinamientos terminan solos y se avisa a cada padrino.
- **Estados del beneficiario** (`Domain/Constantes/ReglasBeneficiario.cs`): "Fallecido" es final; un adoptado solo puede volver a "En la fundación" o pasar a "Fallecido"; de "En tratamiento" no se pasa directo a "Adoptado".
- **Avisos:** además de la notificación en la plataforma, al confirmar o rechazar una donación se envía un correo al donante. Si el correo no está configurado, la acción se hace igual.
- **Región:** por ahora MunerApp funciona solo en Sabana de Occidente. El municipio de la fundación se elige de la lista de `Domain/Constantes/Region.cs` (perfil y `/participar`), y el inicio muestra cuántas fundaciones hay en cada municipio.
- **Al iniciar sesión:** el superadministrador entra a *Administración → Resumen* y el equipo de una fundación a su panel. Si ya tienen sesión y abren el inicio, también los lleva a su panel. Los donantes ven el inicio con un resumen de sus aportes.
- **Superadministrador:** ve cifras e informes de donaciones y apadrinamientos de toda la plataforma, sin datos de los donantes ni de los padrinos: esos los ve solo la fundación que recibe el aporte.
- **Quiero participar:** `/participar` envía la solicitud de una fundación al superadministrador, por notificación y por correo.
- **Voluntarios (decisión para el Sprint 6, HU-036):** al aprobar una postulación, la misma cuenta de la persona pasa a ser voluntaria de esa fundación y deja de poder donar con ella.

## Hoja de vida del beneficiario (formato de El Reino de los Gatos)

La hoja de vida sigue el formato "Historia clínica e ingreso" que usa la fundación en Excel:

| Sección del formato | Dónde está en MunerApp | Quién la registra |
|---|---|---|
| Reseña del paciente: ingreso, edad o fecha de nacimiento, peso, sexo, procedencia, color, raza, estado reproductivo, señales particulares, detalles de procedencia | Registrar o editar la hoja de vida | Admin principal |
| Examen semiológico: FR, FC, TLLC, RPC, T°, condición corporal, mucosas, estado de conciencia y observaciones | Hoja de vida → Examen de ingreso | Admin principal o voluntario de salud |
| Vacunación (vacuna, laboratorio, responsable, fecha), desparasitación (desparasitante, peso) y pruebas virales VIF/FeLV | Historia clínica → Registrar evento | Admin principal o voluntario de salud |
| Fin de reseña: adoptado, liberado, encontró su hogar o falleció | Cambiar estado | Admin principal |
| Datos del adoptante: nombre, teléfono, ciudad, fecha, No. de formulario y quién elaboró | Hoja de vida → Adoptante | Admin principal |
| Etograma antes y después de la castración | Hoja de vida → Comportamiento | Admin principal o voluntario de salud |

El examen de ingreso es información clínica: solo lo ven los administradores y los voluntarios de salud.

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
