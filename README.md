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

## Sprint 4: tienda y boletín

| HU | Historia | Dónde está |
|---|---|---|
| 023 | Gestión de productos (administrador) | `Areas/Fundacion/Controllers/ProductosController.cs` (`/Fundacion/Productos`) |
| 024 | Catálogo en tarjetas (visitante) | `Controllers/TiendaController.cs` (`/tienda`, `/fundaciones/{slug}/tienda`, `/fundaciones/{slug}/tienda/{id}`) |
| 025 | Chat del donante con la tienda | `Controllers/ChatsTiendaController.cs` (`/fundaciones/{slug}/tienda/{id}/chat`, `/mis-chats`) |
| 026 | Atención de chats y pedidos (administrador) | `Areas/Fundacion/Controllers/ChatsTiendaController.cs` y `PedidosController.cs` (`/Fundacion/ChatsTienda`, `/Fundacion/Pedidos`) |
| 027 | Publicaciones del boletín (administrador) | `Areas/Fundacion/Controllers/BoletinController.cs` (`/Fundacion/Boletin`) |
| 028 | Consulta del boletín y eventos (visitante) | `Controllers/BoletinController.cs` (`/boletin`, `/fundaciones/{slug}/boletin`, `/fundaciones/{slug}/boletin/{id}`) |

Migraciones, en orden: `HU023_Productos`, `HU025_ChatTienda`, `HU026_Pedidos`, `HU027_Boletin`.

### Tienda

- La tienda requiere el módulo **Tienda** (configurable). MunerApp no cobra en la tienda: el donante acuerda la compra con la fundación por chat y lo que paga le llega a ella.
- Producto: nombre, descripción, precio, categoría opcional y de 1 a 4 fotos públicas. Estado **Disponible** (se ve y se puede pedir), **Agotado** (se ve, no se puede pedir) u **Oculto** (no se ve). Un producto con chats no se elimina: se oculta, porque los chats y pedidos guardan de qué producto hablaban.
- Crear y editar productos es del administrador principal. Los chats y los pedidos los atiende cualquier administrador de la fundación, como las donaciones.
- Hay **una conversación por donante y producto** (índice único): si vuelve a escribir, continúa la misma. Las cuentas de una fundación no compran como donantes.
- Mensajes sin leer: cada conversación guarda hasta cuándo leyó cada lado (`UltimaLecturaDonante`, `UltimaLecturaFundacion`). Solo se notifica el primer mensaje sin leer, para no llenar de avisos al otro lado. El chat trae los mensajes nuevos cada 8 segundos sin recargar (`site.js`).
- La fundación cierra la conversación cuando termina; si el donante vuelve a escribir, se reabre.
- Pedido: se registra desde el chat (cantidad y precio por unidad; el total se calcula) con código `PED-2026-000001`. Estados: **Acordado → Pagado → Entregado**; antes de entregarse se puede **cancelar**. Cada cambio se le notifica al donante, que ve sus pedidos en el chat.
- Los controladores públicos usan acciones con nombres distintos a `Index` (`MisChats`, `General`, `DeFundacion`...) porque el área `Fundacion` tiene controladores con el mismo nombre (`ChatsTienda`, `Boletin`) y si no, los enlaces del panel se confunden con las rutas públicas.

### Boletín

- El módulo **Boletín** es general (siempre activo). Categorías: Noticia, Evento, Logro y **RendicionCuentas**.
- Una publicación nace como **Borrador** (solo la ve el equipo) o se publica de una vez; puede volver a borrador. Crear, editar y publicar es del administrador principal.
- Un **evento** exige fecha y hora futuras (se escriben en hora de Colombia y se guardan en UTC) y lugar. En el boletín público los próximos eventos se destacan arriba; los pasados quedan marcados. El detalle de un evento futuro tiene "Agregar a mi calendario" (Google Calendar).
- `CausaId` es opcional: liga la publicación a una causa de la fundación. **Para HU-046:** la rendición de cuentas de una causa cerrada se publica como `Publicacion` con `Categoria = RendicionCuentas`, `Estado = Publicada`, `FechaPublicacion` y `CausaId`.
- El perfil de la fundación muestra las 2 publicaciones más recientes con un enlace a todo su boletín.
- **El periódico de MunerApp** (`Servicios/PeriodicoMunerApp.cs`, vista `Shared/_Periodico.cshtml`): portada pública en el inicio y arriba de `/boletin`, sin iniciar sesión. Tiene el titular (el próximo evento de los siguientes 60 días o la publicación más reciente), los próximos eventos, lo último del boletín de todas las fundaciones, las cifras de la plataforma (fundaciones, peludos adoptados, causas cumplidas y total donado) y **buenas noticias que se generan solas** con la actividad de los últimos 90 días: adopciones, causas que llegan a su meta, fundaciones nuevas y productos nuevos en las tiendas. De los donantes solo se publican totales; de las adopciones, el nombre del peludo, nunca el de la familia.
- **Boletín de demostración:** con `Seed:BoletinDemo = true` (ya está en `appsettings.Development.json`), al arrancar la app se crean publicaciones ficticias en cada fundación activa que aún no tenga ninguna: noticias, un logro, un evento pasado y eventos próximos, entre ellos **"Tu gato secreto"** (el amigo secreto, pero con gatos) en diciembre. Las fechas se calculan desde el día en que se siembran. Está en `Infrastructure/Persistence/BoletinDemo.cs`. En producción no se activa.

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
