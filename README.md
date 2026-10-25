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
| Veedor (permiso) | Donante o persona de una veeduría ciudadana a quien el superadmin le da el permiso por correo |

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
| Ver el libro de fondos e inventario de especie | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ | ❌ |
| Registrar gastos, traslados, proyectos y cerrar fondos | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ |
| Registrar usos de la especie | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ | ❌ |
| Veeduría pública (`/veeduria`) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Registro de cambios | ❌ | Solo con permiso de veedor | ❌ | ❌ | ❌ | ❌ | ✅ |

Reglas adicionales:

- Nadie confirma ni rechaza una donación que reportó él mismo.
- Las cuentas de fundación y el superadministrador no donan, no apadrinan y no se postulan: así una fundación no se puede reportar y confirmar donaciones a sí misma. Para apoyar a título personal se usa otra cuenta.
- Ser veedor es un permiso sobre una cuenta normal, no una cuenta aparte: el veedor sigue pudiendo donar. El equipo de una fundación y el superadmin no pueden ser veedores.
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
## Sprint 4: donación en línea (HU-043)

| HU | Historia | Dónde está |
|---|---|---|
| 043 | Donación en línea a una causa con Wompi | `Controllers/PagosWompiController.cs` (`/fundaciones/{slug}/causas/{id}/donar-en-linea`, `/pagos/resultado/{referencia}`) · `Infrastructure/Servicios/WompiService.cs` |

### Donación en línea con Wompi (HU-043)

- En el detalle de una causa que recibe donaciones, si la fundación tiene **Pagos en línea** activos, aparece "Donar en línea". Si no, queda el flujo por transferencia.
- El donante escribe el valor (de $5.000 a $50.000.000, constantes en el controlador). Se crea la `Donacion` en **Pendiente** con el contrato de HU-044 y se envía al Web Checkout de Wompi con la firma de integridad: SHA256 de `referencia + montoEnCentavos + moneda + secretoIntegridad` (el secreto se descifra con `ISecretosService`).
- Al volver del checkout (`/pagos/resultado/{referencia}?id=...`), la página consulta la transacción en el API de Wompi y **solo muestra** el resultado; no cambia la donación. La confirmación oficial, la suma a la causa y el comprobante llegan con el aviso de Wompi (HU-044).
- En **Mis donaciones**, una donación en línea no tiene soporte; mientras está pendiente muestra "Consultar el pago".
- Si el donante abandona el checkout, la donación se queda en Pendiente y nunca suma a la causa.
- **Probar en local:** Wompi responde 403 si la dirección de regreso es `localhost` o `127.0.0.1`. Abre la app desde un túnel público (Dev Tunnels de Visual Studio, ngrok o cloudflared). En Development la app lee `X-Forwarded-Host` para armar la dirección de regreso con el dominio del túnel. El mismo túnel sirve como URL de eventos de HU-044.

## Sprint 5: estado del código

| HU | Historia | Dónde está |
|---|---|---|
| 029 | Recomendaciones y responsabilidades antes de adoptar | `Controllers/AdopcionesController.cs` → `Recomendaciones` (`/fundaciones/{slug}/adoptar`) · configuración de la fundación en `Areas/Fundacion/Controllers/AdopcionesController.cs` |
| 030 | Datos personales y de contacto del adoptante | `AdopcionesController` → `Datos` (`/fundaciones/{slug}/adoptar/formulario/datos`) · `Validacion/ValidadorPersonas.cs` |
| 031 | Sección de mascotas con preguntas condicionales | `AdopcionesController` → `Mascotas`, `Carne` (`/fundaciones/{slug}/adoptar/formulario/mascotas`) |
| 032 | Hogar y compromisos, y envío de la solicitud | `AdopcionesController` → `Hogar` (`/fundaciones/{slug}/adoptar/formulario/hogar`), `MisAdopciones` (`/mis-adopciones`) · aporte y toxoplasmosis en `Areas/Fundacion/Controllers/AdopcionesController.cs` → `Configuracion` |
| 033 | Panel de solicitudes: aprobar para cita o rechazar | `Areas/Fundacion/Controllers/AdopcionesController.cs` → `Index`, `Detalle`, `Aprobar`, `Rechazar` (`/Fundacion/Adopciones`) |
| 034 | Cita presencial: agendar, reprogramar y registrar el resultado | `Areas/Fundacion/Controllers/AdopcionesController.cs` → `AgendarCita`, `RegistrarResultado` · `Servicios/EstadosBeneficiario.cs` |
| 044 | Confirmación automática de donaciones en línea con el aviso de Wompi | `Controllers/WompiController.cs` (`POST /api/wompi/eventos/{esalId}`) · `Servicios/ConfirmacionPagosWompi.cs` · `Infrastructure/Servicios/WompiFirmaEventos.cs` |

### Solicitud de adopción

- La solicitud (`SolicitudAdopcion`) nace en estado **Borrador** cuando la persona acepta las recomendaciones. Una persona solo tiene un borrador por fundación: si vuelve, continúa el mismo.
- El formulario (`/fundaciones/{slug}/adoptar/formulario`) solo se abre si existe ese borrador, así no se puede entrar por URL sin aceptar.
- Tiene 3 secciones (datos personales, mascotas, hogar y compromisos). Cada una guarda el avance en `SeccionesCompletadas`; `/formulario` lleva a la sección donde quedó la persona y no se puede saltar a una sección sin guardar las anteriores.
- Cédula (6 a 10 dígitos) y celulares (10 dígitos, empiezan por 3; se acepta +57) se guardan solo con dígitos. La referencia personal debe tener un celular distinto al del solicitante.
- Mascotas: si tiene, elige gato, perro u otra y responde las preguntas de cada tipo; si tuvo, cuenta qué ocurrió. Solo se guardan las respuestas que aplican. El carné de vacunas del gato es opcional (vacunas a o c), se guarda como archivo **privado** y se elimina si se reemplaza, se quita o deja de aplicar.
- Preguntas condicionales: `<div data-mostrar-si="Campo=Valor1,Valor2">` en las vistas (ver `site.js`). El servidor valida lo mismo, así que ocultar un campo nunca reemplaza la validación.
- Las recomendaciones se leen sin cuenta; para aceptarlas se pide iniciar sesión. Las cuentas de una fundación no pueden solicitar adopciones.
- Cada fundación configura en **Panel → Adopción**: sus recomendaciones (una por línea), el valor del aporte al adoptar y el mensaje e imagen sobre toxoplasmosis. Mientras no los guarde, se usan los predeterminados de `ConfigAdopcion`.
- Hogar y compromisos: si hay niños se pregunta si han interactuado con mascotas; al responder sobre embarazo aparece la información de toxoplasmosis; si la vivienda es arrendada se pregunta si el arrendador permite mascotas.
- Al enviar se exigen el requisito (aporte y huacal), el contrato y la autorización de datos (Ley 1581, con fecha). La solicitud pasa a **Recibida** con su código (`ADO-2026-000001`), se notifica a los administradores de la fundación y la persona la ve en **Mis adopciones**. No puede iniciar otra en la misma fundación mientras esté en trámite.
- La fundación revisa en **Panel → Adopción** (administradores principal y de consulta): pestañas por estado, detalle completo con el carné y botón de WhatsApp. **Aprobar** la deja en *Aprobada para cita*; **rechazar** exige un motivo que ve la persona. En ambos casos se notifica al solicitante. Una solicitud rechazada deja de estar en trámite: la persona puede volver a solicitar.
- Cita (HU-034): a una solicitud aprobada se le agenda fecha, hora (de Colombia; se guarda en UTC) y lugar. La persona recibe la cita con los requisitos (aporte configurado, huacal y documento) y la ve en Mis adopciones. Se puede reprogramar (se cuenta y se avisa). Desde el día de la cita se registra el resultado: si se concretó, el beneficiario elegido pasa a **Adoptado** y se crean sus datos de adoptante con los de la solicitud; si no, se guardan las observaciones y la persona puede volver a solicitar.
- `Servicios/EstadosBeneficiario.cs` concentra el cambio de estado de un beneficiario (historial, retiro del apadrinamiento y aviso a los padrinos). Lo usan la hoja de vida (Sprint 3) y la adopción concretada, para que las reglas estén en un solo lugar.
- La acción pública se llama `MisAdopciones` (no `Index`) para que los enlaces del área `Fundacion` no se confundan con `/mis-adopciones`: ambos controladores se llaman `Adopciones`.
- Requiere el módulo **Adopción** activo en la fundación.
- **Formato de la fundación:** el formulario sigue las 30 preguntas que El Reino de los Gatos definió en la reunión de levantamiento del 9 de septiembre (teléfono adicional, barrio, edad de las mascotas, castrado y vacunas en una sola pregunta con 4 opciones, por qué quiere adoptar, gato que busca, compromiso de 15 años, mudanzas, lugar para la mascota, niños por rango de edad, toxoplasmosis, mejor cualidad, costo mensual y comentarios). Migración `FormularioAdopcionFormato`. Las preguntas separadas de esterilización y vacunas y la de "puede cubrir los costos" ya no se piden; se conservan en la base de datos solo para las solicitudes enviadas antes.

### Confirmación automática con Wompi (HU-044)

- Cada fundación pega en su panel de Wompi (**Desarrolladores → URL de eventos**) la URL que muestra **Panel → Pagos en línea**: `https://{dominio}/api/wompi/eventos/{esalId}`.
- La firma se valida con el secreto de eventos de la fundación: SHA256 de los valores de `signature.properties` + `timestamp` + secreto ([documentación de Wompi](https://docs.wompi.co/docs/colombia/eventos/)). Si no coincide, se responde 401 y no se toca ninguna donación.
- La donación se busca por `ReferenciaPasarela` (la genera HU-043) y debe tener `Origen = Wompi`. `APPROVED` → Confirmada; `DECLINED`, `VOIDED` o `ERROR` → Rechazada; `PENDING` no cambia nada. El valor pagado debe coincidir con el de la donación.
- La donación solo cambia si sigue Pendiente (actualización atómica): un aviso repetido, o varios a la vez, no la procesan dos veces. El recaudado de la causa se calcula con las donaciones confirmadas, así que nunca se suma doble.
- Cada aviso queda en la tabla `EventoPasarela` con su resultado (Procesado, FirmaInvalida, Duplicado, MontoNoCoincide...).
- Una donación en línea no se confirma ni rechaza a mano desde el panel de donaciones.
- En la lista de donaciones se marcan como *En línea* (*esperando a Wompi* mientras siguen pendientes), y el aviso del panel "donaciones por confirmar" solo cuenta transferencias. Si el donante abandona el checkout, la donación queda pendiente y nunca suma a la causa.
- Probado de punta a punta con HU-043 en local: la donación nace pendiente, el aviso aprobado la confirma, la causa suma el valor, el aviso repetido queda como *Duplicado* y el donante recibe su comprobante.
- **Contrato con HU-043:** al iniciar el pago se crea la `Donacion` en Pendiente con `Origen = Wompi`, `ReferenciaPasarela` única, `CausaId`, `MedioPago = "Wompi"` y `SoporteRuta` vacío.
- Para probar en local se necesita una URL pública (por ejemplo, un túnel de Visual Studio o ngrok) registrada como URL de eventos en el sandbox de Wompi.

## Donaciones en especie

- La fundación publica lo que necesita en **Mi fundación > En especie** (comida, arena, medicamentos, aseo, accesorios). Solo el administrador principal crea o pausa necesidades.
- El donante entra a la fundación, elige **Donar en especie**, dice qué lleva, cuánto y si lo deja en la sede o lo recogen. Recibe un código `ESP-año-número`.
- Cuando llega, el equipo marca **Recibida** con la cantidad real, vencimiento si aplica y una foto. El donante ve el estado en **Mis donaciones** y puede bajar su comprobante en PDF.
- Si no llega, se marca **No recibida** con el motivo. El donante puede cancelar mientras siga en Ofrecida.
- El superadmin tiene el informe **En especie** con lo recibido por fundación.

## Veeduría ciudadana

- **Pública:** `/veeduria` la ve cualquiera, sin cuenta. Por fundación muestra el libro de fondos (saldos, gastos y traslados con su soporte), causas y su rendición de cuentas, dinero recibido sin datos del donante, lo recibido en especie con foto y en qué se usó, y los documentos publicados.
- Alertas: causas cerradas sin rendición, donaciones sin revisar por más de 7 días, fundaciones sin documentos y fundaciones que recibieron dinero y no registraron gastos.
- CSV de dinero, especie (con usado y disponible), causas y movimientos de los fondos.
- **Permiso de veedor:** el superadmin lo da en **Veeduría** con el correo. Si la cuenta ya existe se le agrega el permiso y se le avisa; si no, se crea y le llega el correo para poner su clave. No se le puede dar a cuentas de una fundación ni al superadmin. Quitar el permiso no desactiva la cuenta.
- El permiso solo agrega el **registro de cambios** (`/Veeduria/RegistroCambios`), sin datos personales de donantes. Cada consulta del veedor queda anotada en el mismo registro (acción *Consultado*): quien vigila también deja rastro.
- El veedor no ve los datos internos de las fundaciones: el filtro por ESAL lo trata como a cualquier donante.

## Libro de fondos (ciclo de vida de la donación)

Recepción → Asignación → Movimientos → Destinación → Veeduría.

- **Recepción:** la donación confirmada (dinero) o recibida (especie).
- **Asignación:** cada donación confirmada entra sola a un fondo, sin guardar nada extra: la de una causa al fondo de esa causa, la de un padrino a *Apadrinamientos*, las demás al *Fondo general*. La fundación también crea fondos de **proyecto** (empiezan en cero).
- **Movimientos:** traslados entre fondos (dos movimientos, uno en cada fondo, que se anulan juntos).
- **Destinación:** gastos con valor, concepto, fecha, proveedor y soporte público (factura, recibo o foto). No se puede gastar más del saldo. El saldo no se guarda: donaciones + traslados que llegan − gastos − traslados que salen.
- Nada se edita ni se borra: se **anula** con un motivo y sigue visible tachado. Un fondo de causa o de proyecto se cierra solo con saldo en cero.
- El dinero se sigue **por fondo**, no peso por peso. El donante ve en su donación el **recorrido**: a qué fondo entró, qué parte del fondo es su aporte y los últimos gastos.
- **Especie:** cada donación recibida es un **lote**. En el detalle se registran los usos (cantidad, en qué, fecha, foto). Disponible = recibido − usos. **Inventario** muestra lo que queda y alerta lo que vence en 30 días. El donante ve los usos en su donación.
- Dos personas no pueden gastar el mismo saldo a la vez: cada movimiento toma un bloqueo por fundación (`sp_getapplock`).
- Código: `Servicios/LibroFondos.cs`, `Areas/Fundacion/Controllers/FondosController.cs`, `EspecieController` (`RegistrarUso`, `AnularUso`, `Inventario`). Migración `LibroDeFondos`: tablas `Fondo`, `MovimientoFondo` y `SalidaEspecie`.

## Pruebas automatizadas

```bash
dotnet test
```

`tests/MunerApp.Tests` (xUnit) prueba la validación de la firma de los eventos de Wompi la generación de las dosis de un tratamiento de la agenda de salud las reglas de las alertas de salud y cuándo se cierra una causa.

## Sprint 6: estado del código

| HU | Historia | Dónde está |
|---|---|---|
| 036 | Aprobar o rechazar postulaciones y desvincular voluntarios | `Areas/Fundacion/Controllers/PostulacionesController.cs` → `Aprobar`, `Rechazar`, `Desvincular` (`/Fundacion/Postulaciones`) |
| 037 | Inventario clínico de medicamentos e insumos | `Areas/Fundacion/Controllers/MedicamentosController.cs` (`/Fundacion/Medicamentos`) |
| 040 | Reporte de medicamentos con filtros y PDF | `MedicamentosController` → `Reporte`, `ReportePdf` · `Infrastructure/Servicios/ReportesPdfService.cs` |
| 046 | Cierre automático de causas y rendición de cuentas | `Servicios/CierreCausas.cs` (revisión diaria) · `Servicios/RendicionCuentas.cs` · `Areas/Fundacion/Controllers/CausasController.cs` → `Cerrar`, `Rendicion` |
| 022 | Novedades del apadrinado para sus padrinos | `Areas/Fundacion/Controllers/NovedadesController.cs` (`/Fundacion/Novedades/Index/{beneficiarioId}`) · padrinos en `Controllers/ApadrinamientosController.cs` (`/mis-apadrinamientos/{id}`) |
| 039 | Alertas de salud: por vencer, stock bajo y eventos de mañana | `Servicios/AlertasSalud.cs` · revisión diaria en `Servicios/RevisionDiaria.cs` · plazo en `/Fundacion/Medicamentos/Alertas` |
| 038 | Agenda de salud: eventos, tratamientos recurrentes y registro en la historia clínica | `Areas/Fundacion/Controllers/AgendaController.cs` (`/Fundacion/Agenda`) · `Domain/Entities/EventoAgenda.cs` |

### Voluntarios (HU-036)

- Solo el **administrador principal** aprueba, rechaza o desvincula, porque da o quita acceso al panel. El de consulta solo ve.
- **Aprobar:** la persona recibe el rol `Voluntario` con perfil `Salud` (si se postuló como practicante) o `General`, y queda vinculada a la fundación (`Usuario.EsalId`). Conserva el rol `Donante` para seguir donando y aparece en **Equipo**. No se le cambia el sello de seguridad: su sesión se actualiza sola en máximo un minuto (HU-008), sin volver a iniciar sesión.
- **Rechazar:** exige un motivo que la persona ve en Mis postulaciones; sigue como donante y puede volver a postularse.
- **Desvincular:** pierde el rol `Voluntario` y la fundación, conserva `Donante`, y se cambia su sello de seguridad para que pierda el acceso aunque tenga la sesión abierta.
- Una cuenta pertenece a una sola fundación: si la persona ya es parte de otra, no se puede aprobar.
- Los enlaces del panel de voluntarios indican el área (`Fundacion`) de forma explícita: sin ella, `Index` se confunde con la acción pública `/mis-postulaciones`, que está en un controlador con el mismo nombre.

### Inventario de medicamentos (HU-037)

- Requiere el módulo **Salud** y la política `AccesoClinico`: administradores y voluntarios de salud. El voluntario general no tiene acceso ni ve el enlace.
- La cantidad solo cambia con movimientos (registro inicial, entradas y usos), que quedan en `MovimientoMedicamento` con la cantidad resultante. Un uso se puede asociar a un beneficiario.
- La cantidad se actualiza en la base de datos de forma atómica dentro de una transacción: varios usos simultáneos nunca dejan el inventario en negativo.
- Las cantidades se leen con `Formatos.LeerCantidad` (acepta coma o punto, máximo dos decimales), porque los navegadores envían el punto aunque la cultura sea es-CO.
- `CantidadMinima` (opcional) define cuándo hay stock bajo; la usarán las alertas de HU-039.
- Reporte (HU-040), solo administradores: filtros *Vencidos*, *Por vencer* (vencen en los próximos `Medicamento.DiasPorVencerPredeterminado` días) y *Stock bajo*, con descarga en PDF horizontal. Las reglas están en `Medicamento` (`EstaVencido`, `EstaPorVencer`, `TieneStockBajo`) para que pantalla, PDF y alertas coincidan. Los PDF de reportes van en `IReportesService`.
- Las entradas y los usos pasan por `Servicios/InventarioMedicamentos.cs`, que también usa la agenda de salud al descontar una dosis.

### Agenda de salud (HU-038)

- Requiere los módulos **Beneficiarios** y **Salud** y la política `AccesoClinico`.
- Cada evento (`EventoAgenda`) tiene tipo (los mismos de la historia clínica), fecha y hora (UTC), descripción, medicamento del inventario y dosis opcionales, y estado *Pendiente*, *Realizado* o *Cancelado*.
- **Agenda general** (`/Fundacion/Agenda`): pestañas *Próximos 7 días*, *Atrasados*, *Más adelante* y *Realizados*, agrupadas por día en hora de Colombia. **Agenda del beneficiario**: se abre desde su hoja de vida o su historia clínica.
- **Tratamiento recurrente:** frecuencia (cada 1–72 horas o 1–90 días) y duración (1–365 días). `EventoAgenda.FechasDeTratamiento` genera un evento por dosis, todos con el mismo `SerieId` (cada 8 horas durante 5 días son 15 dosis). Máximo `EventoAgenda.MaxDosisPorTratamiento` dosis. *Suspender tratamiento* cancela esa dosis y las siguientes; las anteriores no cambian.
- **Marcar realizado:** crea el `EventoClinico` (HU-019) con lo programado, la dosis del tratamiento y las observaciones, y guarda su id en `EventoAgenda.EventoClinicoId`. Si se indica, descuenta la cantidad del inventario. Todo va en una transacción: si no hay inventario suficiente no se registra nada, y si dos personas marcan el mismo evento solo cuenta el primero.
- Un beneficiario adoptado o fallecido no tiene agenda: al cambiar a esos estados (`EstadosBeneficiario.CambiarAsync`, también desde la adopción concretada) se cancelan sus eventos pendientes.
- No se programan eventos en horas pasadas (15 minutos de margen) ni con medicamentos vencidos.

### Alertas de salud (HU-039)

- Llegan como notificación a los **responsables de la salud**: administradores activos y voluntarios con perfil `Salud` de la fundación (`INotificacionService.AgregarAResponsablesSaludAsync`). El panel muestra además un resumen en vivo (sección *Salud*).
- **Por vencer:** en la revisión diaria, los medicamentos que vencen dentro del plazo de la fundación (`ConfigSalud.DiasAvisoVencimiento`, 30 días por defecto; lo cambia el administrador principal en *Medicamentos → Alertas*). Ese mismo plazo define "Por vencer" en el inventario y en el reporte de HU-040. Se avisa una vez por fecha de vencimiento (`Medicamento.VencimientoAvisado`); si la fecha se corrige, se vuelve a avisar.
- **Stock bajo:** al registrar un uso (desde el inventario o desde la agenda) que deja la cantidad en el mínimo o por debajo. Solo al cruzar el mínimo, no en cada uso. También avisa si se agota.
- **Eventos de mañana:** en la revisión diaria, un resumen de los eventos pendientes del día siguiente (`EventoAgenda.RecordatorioEnviado` evita repetirlo).
- Las notificaciones y las marcas se guardan en el mismo `SaveChanges`: no hay avisos repetidos ni perdidos.

### Novedades del apadrinado (HU-022)

- El administrador publica una novedad (texto de 10 a 2000 caracteres y hasta 3 fotos) desde la hoja de vida del beneficiario, en la tarjeta de *Padrinos*. Se puede publicar si tiene padrinos activos o si se ofrece para apadrinar; también se puede eliminar una novedad publicada por error (sus fotos se borran).
- Los padrinos activos reciben una notificación (`EstadosBeneficiario.NotificarPadrinosAsync`) y ven las novedades en *Mis apadrinamientos*, de la más reciente a la más antigua. Sin novedades se muestra "Pronto habrá noticias".
- Un padrino activo ve todas las novedades, también las anteriores a su llegada. Si canceló, ve solo las publicadas hasta el día en que canceló.
- Las fotos son privadas (`priv/esal/{id}/novedades`): las entrega `ApadrinamientosController.FotoNovedad`, que verifica que el apadrinamiento sea del usuario y que la novedad le corresponda, y `NovedadesController.Foto` para los administradores.

### Cierre de causas y rendición de cuentas (HU-046)

- **Cierre automático:** la tarea `CierreCausas` de la revisión diaria guarda como *Cerrada* la causa que alcanzó su meta o pasó su fecha límite (también las pausadas), con `FechaCierre` y `MotivoCierre` (`Causa.MotivoDeCierre`; si se cumplen ambas, gana la meta). Avisa a los administradores para que publiquen la rendición. El administrador principal también puede cerrar una causa antes de tiempo (*Causas → Cerrar*). Una causa cerrada no se edita ni se reabre.
- **Rendición de cuentas:** en *Causas → Publicar rendición de cuentas* (solo causas cerradas, una por causa): texto de 30 a 4000 caracteres, valor usado opcional, hasta 5 fotos y 5 soportes (PDF, JPG o PNG); al menos una foto o un soporte. Al publicarla, `RendicionCuentas` en una transacción:
  - la muestra en la página de la causa (sección *Rendición de cuentas*, con fotos y soportes descargables);
  - genera un PDF (`IReportesService.GenerarRendicionCausa`) y lo publica en **Transparencia** con la categoría *Rendición de cuentas*, enlazado en `Causa.RendicionDocumentoId`;
  - crea la publicación del **boletín** (HU-027) con la categoría *Rendición de cuentas* y `CausaId`, con su propia copia de la primera foto;
  - **avisa a los donantes** con donaciones confirmadas en la causa.
- Lo recaudado y el número de donaciones se guardan en la rendición al publicarla, para que el registro no cambie después. Los archivos son públicos: el formulario lo advierte.
- Si algo falla al publicar, los archivos ya guardados se borran y no queda nada a medias.
- Los enlaces y redirecciones del panel de causas indican el área (`Fundacion`): sin ella llevaban a la página pública `/causas`, porque hay un controlador público con el mismo nombre (venía del Sprint 3).

### Revisión diaria (HU-039, HU-046)

- `RevisionDiariaService` (un `BackgroundService`) revisa cada 30 minutos si ya pasó la hora configurada y ejecuta una vez por día cada `ITareaDiaria` registrada. Hoy son dos: `AlertasSalud` (HU-039) y `CierreCausas` (HU-046).
- La tabla `RevisionDiaria` (llave fecha + tarea) garantiza que cada tarea corra una sola vez por día aunque la aplicación se reinicie o haya varias instancias. Si una ejecución se cae a medias, se retoma pasados 30 minutos.
- Si la aplicación estaba apagada a la hora de la revisión, corre apenas vuelva a encender ese día.
- Configuración opcional en `appsettings.json`:

```json
"RevisionDiaria": { "Activa": true, "Hora": 6 }
```

- **Azure App Service:** activar *Always On* (Configuración → Configuración general). Sin eso, la aplicación se duerme cuando no hay visitas y la revisión espera a la siguiente visita.
- Para probar o hacer una demostración: *Medicamentos → Alertas → Revisar ahora* ejecuta la revisión de la fundación sin repetir avisos.

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

Los usuarios de una ESAL (administradores y voluntarios) solo ven los datos de su fundación. Las páginas públicas (incluida la veeduría) filtran explícitamente por la ESAL que se consulta. El superadministrador puede usar `IgnoreQueryFilters()` en el panel de plataforma; las páginas públicas también, filtrando siempre de forma explícita (ver Sprint 2).

## Despliegue en Azure

- La app vive en una **App Service Linux (plan B1, .NET 8)** y la base de datos en **Azure SQL**. Se paga con el crédito de Azure for Students.
- Cada vez que se fusiona algo en `develop`, GitHub Actions (`.github/workflows/desplegar-azure.yml`) compila, corre las pruebas y publica. Si una prueba falla, no se publica.
- Al arrancar, la app aplica sola las migraciones pendientes (`BaseDatos:MigrarAlIniciar`). Los datos no se borran al publicar.
- Los archivos subidos y las llaves de cifrado se guardan en `/home/data`, que no se borra al publicar.

**Configuración de la App Service** (Configuración > Variables de entorno):

| Nombre | Valor |
|---|---|
| `ConnectionStrings__DefaultConnection` | Cadena ADO.NET de Azure SQL, con la contraseña |
| `BaseDatos__MigrarAlIniciar` | `true` |
| `Archivos__RutaBase` | `/home/data/archivos` |
| `DataProtection__RutaLlaves` | `/home/data/llaves` |
| `Seed__SuperAdmin__Email` y `Seed__SuperAdmin__Password` | Cuenta del superadministrador |
| `Correo__Usuario`, `Correo__Contrasena`, `Correo__Remitente` | Gmail con contraseña de aplicación |
| `Autenticacion__Google__ClientId` y `Autenticacion__Google__ClientSecret` | Login con Google (opcional) |

- Activar **Always On** (Configuración > Configuración general) para que la revisión diaria se ejecute.
- En GitHub (Settings > Secrets and variables > Actions): secreto `AZURE_WEBAPP_PUBLISH_PROFILE` con el perfil de publicación y variable `AZURE_WEBAPP_NAME` con el nombre de la App Service. Para descargar el perfil hay que activar *SCM Basic Auth Publishing Credentials* en Configuración > Configuración general.
- En Google Cloud, agregar `https://<app>.azurewebsites.net/signin-google` a las URI de redirección. En Wompi, cada fundación copia su URL de eventos desde **Pagos en línea** (`https://<app>.azurewebsites.net/api/wompi/eventos/{esalId}`).

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
