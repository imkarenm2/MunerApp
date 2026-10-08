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

### Confirmación automática con Wompi (HU-044)

- Cada fundación pega en su panel de Wompi (**Desarrolladores → URL de eventos**) la URL que muestra **Panel → Pagos en línea**: `https://{dominio}/api/wompi/eventos/{esalId}`.
- La firma se valida con el secreto de eventos de la fundación: SHA256 de los valores de `signature.properties` + `timestamp` + secreto ([documentación de Wompi](https://docs.wompi.co/docs/colombia/eventos/)). Si no coincide, se responde 401 y no se toca ninguna donación.
- La donación se busca por `ReferenciaPasarela` (la genera HU-043) y debe tener `Origen = Wompi`. `APPROVED` → Confirmada; `DECLINED`, `VOIDED` o `ERROR` → Rechazada; `PENDING` no cambia nada. El valor pagado debe coincidir con el de la donación.
- La donación solo cambia si sigue Pendiente (actualización atómica): un aviso repetido, o varios a la vez, no la procesan dos veces. El recaudado de la causa se calcula con las donaciones confirmadas, así que nunca se suma doble.
- Cada aviso queda en la tabla `EventoPasarela` con su resultado (Procesado, FirmaInvalida, Duplicado, MontoNoCoincide...).
- Una donación en línea no se confirma ni rechaza a mano desde el panel de donaciones.
- **Contrato con HU-043:** al iniciar el pago se crea la `Donacion` en Pendiente con `Origen = Wompi`, `ReferenciaPasarela` única, `CausaId`, `MedioPago = "Wompi"` y `SoporteRuta` vacío.
- Para probar en local se necesita una URL pública (por ejemplo, un túnel de Visual Studio o ngrok) registrada como URL de eventos en el sandbox de Wompi.

## Pruebas automatizadas

```bash
dotnet test
```

`tests/MunerApp.Tests` (xUnit) prueba la validación de la firma de los eventos de Wompi.

## Sprint 6: estado del código

| HU | Historia | Dónde está |
|---|---|---|
| 036 | Aprobar o rechazar postulaciones y desvincular voluntarios | `Areas/Fundacion/Controllers/PostulacionesController.cs` → `Aprobar`, `Rechazar`, `Desvincular` (`/Fundacion/Postulaciones`) |
| 037 | Inventario clínico de medicamentos e insumos | `Areas/Fundacion/Controllers/MedicamentosController.cs` (`/Fundacion/Medicamentos`) |
| 040 | Reporte de medicamentos con filtros y PDF | `MedicamentosController` → `Reporte`, `ReportePdf` · `Infrastructure/Servicios/ReportesPdfService.cs` |

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
