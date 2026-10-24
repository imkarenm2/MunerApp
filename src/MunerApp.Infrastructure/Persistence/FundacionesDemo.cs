using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Constantes;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Identity;

namespace MunerApp.Infrastructure.Persistence;

/// <summary>
/// Cinco fundaciones de ejemplo de distintos temas (animales, adulto mayor, comedor comunitario, niñez y medio ambiente),
/// con causas, donaciones confirmadas, actividades próximas en el boletín, necesidades en especie y productos,
/// para que en desarrollo y en las demos la página no se vea vacía.
/// Son ficticias: se crean con Seed:FundacionesDemo = true (activo en appsettings.json mientras la página se usa
/// solo para presentar el proyecto). ANTES DE ABRIR LA PÁGINA AL PÚBLICO: ponerlo en false y desactivar estas fundaciones.
/// Las cuentas de sus administradores (admin@{slug}.test) solo se crean si se configura Seed:FundacionesDemoContrasena:
/// en local con dotnet user-secrets y en Azure como configuración del App Service. El repositorio es público:
/// la contraseña nunca va en appsettings.
/// Los datos para donar son de ejemplo y lo dicen. Las fechas se calculan desde el día en que se crean.
/// Las imágenes son fotos de bancos libres (ver FotosDemo); el logo se deja vacío y se muestran las iniciales.
/// Si una fundación de ejemplo ya existe (por su slug), no se vuelve a crear: solo se cambian sus ilustraciones
/// antiguas (.svg) por las fotos.
/// </summary>
public static class FundacionesDemo
{
    private static readonly TimeSpan DiferenciaColombia = TimeSpan.FromHours(5); // Colombia es UTC-5 todo el año
    private const int ModuloTienda = 6;

    // "Foto" es el nombre del archivo en wwwroot/img/fotos, sin la extensión .jpg
    private record CausaDemo(string Titulo, string Descripcion, decimal Meta, decimal Recaudado, int DiasRestantes,
        string Foto, bool Cerrada = false);

    private record EventoDemo(CategoriaPublicacion Categoria, string Titulo, string Resumen, string Contenido,
        int? DiasHastaEvento, int Hora, string? Lugar, int DiasDesdePublicacion, string Foto);

    private record NecesidadDemo(string Nombre, CategoriaEspecie Categoria, string Unidad, int Meta, string Descripcion, bool Urgente);

    private record ProductoDemo(string Nombre, string Descripcion, decimal Precio, string Categoria, string Foto, bool Agotado = false);

    private record FundacionDemo(string Slug, string Nombre, string Nit, string Tipo, string Ciudad, string Telefono,
        string DescripcionCorta, string Historia, string Mision, string Vision, string Foto,
        CausaDemo[] Causas, EventoDemo[] Publicaciones, NecesidadDemo[] Necesidades, ProductoDemo[] Productos);

    private static readonly (string Nombre, string Correo)[] Donantes =
    {
        ("Laura Méndez", "laura.mendez"), ("Andrés Rojas", "andres.rojas"), ("Camila Torres", "camila.torres"),
        ("Jorge Castillo", "jorge.castillo"), ("Paola Ramírez", "paola.ramirez"), ("Diego Herrera", "diego.herrera"),
        ("Valentina Gómez", "valentina.gomez"), ("Óscar Pineda", "oscar.pineda")
    };

    private static readonly string[] Mensajes =
    {
        "¡Mucha fuerza con esta causa!", "Un granito de arena de parte de mi familia.", "Gracias por lo que hacen.",
        "Ojalá lleguen pronto a la meta.", "Con cariño desde Facatativá.", "Para que sigan ayudando."
    };

    /// <summary>Administrador principal de cada fundación de ejemplo (correo admin@{slug}.test).</summary>
    private static readonly Dictionary<string, string> Administradores = new()
    {
        ["huellitas-de-funza"] = "Marcela Quintero",
        ["abuelos-de-oro-mosquera"] = "Hernán Salazar",
        ["comedor-pan-y-esperanza"] = "Gloria Patiño",
        ["semillas-de-sabana"] = "Natalia Bermúdez",
        ["guardianes-del-agua"] = "Sebastián Lozano"
    };

    public static async Task SembrarAsync(MunerAppDbContext db, UserManager<Usuario> usuarios, IAlmacenamientoArchivos archivos,
        string carpetaFotos, string? contrasenaAdmins)
    {
        var existentes = await db.Esales.Where(e => e.Slug != null).Select(e => e.Slug!).ToListAsync();
        var nuevas = Fundaciones().Where(f => !existentes.Contains(f.Slug)).ToList();
        if (nuevas.Count > 0) await CrearFundacionesAsync(db, usuarios, archivos, carpetaFotos, nuevas);
        await CambiarIlustracionesPorFotosAsync(db, archivos, carpetaFotos, Fundaciones().Where(f => existentes.Contains(f.Slug)).ToList());
        if (!string.IsNullOrWhiteSpace(contrasenaAdmins)) await CrearAdministradoresAsync(db, usuarios, contrasenaAdmins);
    }

    /// <summary>
    /// Las fundaciones de ejemplo creadas con la versión anterior tenían ilustraciones (.svg con emojis):
    /// se cambian por las fotos, buscando cada causa, publicación y producto por su título.
    /// </summary>
    private static async Task CambiarIlustracionesPorFotosAsync(MunerAppDbContext db, IAlmacenamientoArchivos archivos,
        string carpetaFotos, List<FundacionDemo> existentes)
    {
        foreach (var f in existentes)
        {
            var esal = await db.Esales.FirstAsync(e => e.Slug == f.Slug);
            if (FotosDemo.EsIlustracion(esal.LogoRuta)) esal.LogoRuta = null;

            foreach (var foto in await db.FotosEsal.IgnoreQueryFilters().Where(x => x.EsalId == esal.Id).ToListAsync())
                if (FotosDemo.EsIlustracion(foto.Ruta))
                    foto.Ruta = await FotosDemo.GuardarAsync(archivos, carpetaFotos, $"esal/{esal.Id}/perfil", f.Foto) ?? foto.Ruta;

            var causas = await db.Causas.IgnoreQueryFilters().Where(x => x.EsalId == esal.Id).ToListAsync();
            foreach (var foto in await db.FotosCausa.IgnoreQueryFilters().Where(x => x.EsalId == esal.Id).ToListAsync())
            {
                var demo = f.Causas.FirstOrDefault(c => c.Titulo == causas.FirstOrDefault(x => x.Id == foto.CausaId)?.Titulo);
                if (demo is not null && FotosDemo.EsIlustracion(foto.Ruta))
                    foto.Ruta = await FotosDemo.GuardarAsync(archivos, carpetaFotos, $"esal/{esal.Id}/causas", demo.Foto) ?? foto.Ruta;
            }

            foreach (var p in await db.Publicaciones.IgnoreQueryFilters().Where(x => x.EsalId == esal.Id).ToListAsync())
            {
                var demo = f.Publicaciones.FirstOrDefault(x => x.Titulo == p.Titulo);
                if (demo is not null && FotosDemo.EsIlustracion(p.ImagenRuta))
                    p.ImagenRuta = await FotosDemo.GuardarAsync(archivos, carpetaFotos, $"esal/{esal.Id}/boletin", demo.Foto) ?? p.ImagenRuta;
            }

            var productos = await db.Productos.IgnoreQueryFilters().Where(x => x.EsalId == esal.Id).ToListAsync();
            foreach (var foto in await db.FotosProducto.IgnoreQueryFilters().Where(x => x.EsalId == esal.Id).ToListAsync())
            {
                var demo = f.Productos.FirstOrDefault(x => x.Nombre == productos.FirstOrDefault(p => p.Id == foto.ProductoId)?.Nombre);
                if (demo is not null && FotosDemo.EsIlustracion(foto.Ruta))
                    foto.Ruta = await FotosDemo.GuardarAsync(archivos, carpetaFotos, $"esal/{esal.Id}/tienda", demo.Foto) ?? foto.Ruta;
            }
        }
        await db.SaveChangesAsync();
    }

    /// <summary>Una cuenta de administrador principal por fundación de ejemplo, para entrar a su panel en las pruebas.</summary>
    private static async Task CrearAdministradoresAsync(MunerAppDbContext db, UserManager<Usuario> usuarios, string contrasena)
    {
        foreach (var (slug, nombre) in Administradores)
        {
            var esalId = await db.Esales.Where(e => e.Slug == slug).Select(e => (int?)e.Id).FirstOrDefaultAsync();
            var email = $"admin@{slug}.test";
            if (esalId is null || await usuarios.FindByEmailAsync(email) is not null) continue;

            var admin = new Usuario
            {
                UserName = email, Email = email, EmailConfirmed = true, NombreCompleto = nombre,
                EsalId = esalId, Perfil = Perfiles.Principal
            };
            if ((await usuarios.CreateAsync(admin, contrasena)).Succeeded)
                await usuarios.AddToRoleAsync(admin, Roles.AdministradorESAL);
        }
    }

    private static async Task CrearFundacionesAsync(MunerAppDbContext db, UserManager<Usuario> usuarios, IAlmacenamientoArchivos archivos,
        string carpetaFotos, List<FundacionDemo> nuevas)
    {
        var donantes = await DonantesAsync(usuarios);
        var hoy = (DateTime.UtcNow - DiferenciaColombia).Date;
        var ahora = DateTime.UtcNow;
        var azar = new Random(2026);
        var consecutivo = await db.Donaciones.IgnoreQueryFilters().CountAsync(d => d.Codigo.StartsWith("DEMO-"));

        foreach (var f in nuevas)
        {
            var esal = new Esal
            {
                Nombre = f.Nombre,
                Nit = f.Nit,
                TipoEntidad = f.Tipo,
                CorreoContacto = $"contacto@{f.Slug}.ejemplo.org",
                Slug = f.Slug,
                DescripcionCorta = f.DescripcionCorta,
                Historia = f.Historia,
                Mision = f.Mision,
                Vision = f.Vision,
                Ciudad = f.Ciudad,
                Telefono = f.Telefono,
                FechaRegistro = ahora.AddDays(-azar.Next(60, 200)),
                FechaActualizacionPerfil = ahora.AddDays(-azar.Next(1, 20))
            };
            db.Esales.Add(esal);
            await db.SaveChangesAsync();

            // Sin logo: se muestran las iniciales de la fundación. La foto de perfil se usa como portada.
            if (await FotosDemo.GuardarAsync(archivos, carpetaFotos, $"esal/{esal.Id}/perfil", f.Foto) is { } fotoEsal)
                db.FotosEsal.Add(new FotoEsal { EsalId = esal.Id, Ruta = fotoEsal, Orden = 0 });

            db.DatosDonacion.Add(new DatosDonacion
            {
                EsalId = esal.Id,
                Titular = f.Nombre,
                DocumentoTitular = f.Nit,
                Entidad = "Nequi",
                TipoCuenta = TipoCuentaDonacion.Llave,
                TipoLlave = Domain.Enums.TipoLlave.Alfanumerica,
                Numero = "@EJEMPLO-" + f.Slug.Split('-')[0].ToUpperInvariant(),
                Instrucciones = "Fundación de ejemplo para mostrar cómo funciona MunerApp. Por favor no transfieras dinero a esta llave."
            });

            db.RedesSociales.Add(new RedSocial { EsalId = esal.Id, Tipo = TipoRedSocial.Instagram, Url = $"https://www.instagram.com/{f.Slug.Replace("-", "")}" });

            // ---- Causas con sus donaciones confirmadas ----
            foreach (var c in f.Causas)
            {
                var fechaLimite = hoy.AddDays(c.DiasRestantes);
                var causa = new Causa
                {
                    EsalId = esal.Id,
                    Titulo = c.Titulo,
                    Descripcion = c.Descripcion,
                    Meta = c.Meta,
                    FechaLimite = fechaLimite,
                    Estado = c.Cerrada ? EstadoCausa.Cerrada : EstadoCausa.Activa,
                    FechaCierre = c.Cerrada ? fechaLimite.Add(DiferenciaColombia) : null,
                    MotivoCierre = c.Cerrada ? (c.Recaudado >= c.Meta ? MotivoCierreCausa.MetaAlcanzada : MotivoCierreCausa.FechaLimite) : null,
                    FechaCreacion = ahora.AddDays(-Math.Max(15, 45 - c.DiasRestantes)),
                    FechaRevision = ahora.AddDays(-Math.Max(14, 44 - c.DiasRestantes))
                };
                db.Causas.Add(causa);
                await db.SaveChangesAsync();

                if (await FotosDemo.GuardarAsync(archivos, carpetaFotos, $"esal/{esal.Id}/causas", c.Foto) is { } fotoCausa)
                    db.FotosCausa.Add(new FotoCausa { EsalId = esal.Id, CausaId = causa.Id, Orden = 0, Ruta = fotoCausa });

                var desde = causa.FechaCreacion;
                var hasta = c.Cerrada ? causa.FechaCierre!.Value : ahora;
                foreach (var valor in Repartir(c.Recaudado, azar))
                    db.Donaciones.Add(NuevaDonacion(esal.Id, causa.Id, valor, donantes, desde, hasta, azar, ref consecutivo));
            }

            // Algunas donaciones generales a la fundación (sin causa)
            for (var i = 0; i < 4; i++)
                db.Donaciones.Add(NuevaDonacion(esal.Id, null, azar.Next(2, 16) * 10_000m, donantes, ahora.AddDays(-60), ahora, azar, ref consecutivo));

            // ---- Boletín: actividades próximas, logros y noticias ----
            foreach (var p in f.Publicaciones)
            {
                DateTime? evento = p.DiasHastaEvento is int dias ? hoy.AddDays(dias).AddHours(p.Hora) + DiferenciaColombia : null;
                db.Publicaciones.Add(new Publicacion
                {
                    EsalId = esal.Id,
                    Titulo = p.Titulo,
                    Resumen = p.Resumen,
                    Contenido = p.Contenido,
                    Categoria = p.Categoria,
                    Estado = EstadoPublicacion.Publicada,
                    FechaEvento = evento,
                    LugarEvento = p.Lugar,
                    FechaCreacion = ahora.AddDays(-p.DiasDesdePublicacion),
                    FechaPublicacion = ahora.AddDays(-p.DiasDesdePublicacion),
                    ImagenRuta = await FotosDemo.GuardarAsync(archivos, carpetaFotos, $"esal/{esal.Id}/boletin", p.Foto)
                });
            }

            // ---- Donaciones en especie ----
            foreach (var n in f.Necesidades)
                db.NecesidadesEspecie.Add(new NecesidadEspecie
                {
                    EsalId = esal.Id, Nombre = n.Nombre, Categoria = n.Categoria, Unidad = n.Unidad,
                    CantidadMeta = n.Meta, Descripcion = n.Descripcion, Urgente = n.Urgente,
                    FechaCreacion = ahora.AddDays(-azar.Next(2, 30))
                });

            // ---- Tienda ----
            if (f.Productos.Length > 0)
            {
                db.EsalModulos.Add(new EsalModulo { EsalId = esal.Id, ModuloId = ModuloTienda, Activo = true });
                foreach (var pr in f.Productos)
                {
                    var producto = new Producto
                    {
                        EsalId = esal.Id, Nombre = pr.Nombre, Descripcion = pr.Descripcion, Precio = pr.Precio,
                        Categoria = pr.Categoria, Estado = pr.Agotado ? EstadoProducto.Agotado : EstadoProducto.Disponible,
                        FechaCreacion = ahora.AddDays(-azar.Next(5, 40))
                    };
                    db.Productos.Add(producto);
                    await db.SaveChangesAsync();
                    if (await FotosDemo.GuardarAsync(archivos, carpetaFotos, $"esal/{esal.Id}/tienda", pr.Foto) is { } fotoProducto)
                        db.FotosProducto.Add(new FotoProducto { EsalId = esal.Id, ProductoId = producto.Id, Orden = 0, Ruta = fotoProducto });
                }
            }

            await db.SaveChangesAsync();
        }
    }

    /// <summary>Donantes ficticios (sin contraseña: no pueden iniciar sesión) para que las causas tengan aportes.</summary>
    private static async Task<List<string>> DonantesAsync(UserManager<Usuario> usuarios)
    {
        var ids = new List<string>();
        foreach (var (nombre, correo) in Donantes)
        {
            var email = $"{correo}@donantes.ejemplo.org";
            var usuario = await usuarios.FindByEmailAsync(email);
            if (usuario is null)
            {
                usuario = new Usuario { UserName = email, Email = email, EmailConfirmed = true, NombreCompleto = nombre, FechaRegistro = DateTime.UtcNow.AddDays(-90) };
                var resultado = await usuarios.CreateAsync(usuario);
                if (!resultado.Succeeded) continue;
                await usuarios.AddToRoleAsync(usuario, Roles.Donante);
            }
            ids.Add(usuario.Id);
        }
        return ids;
    }

    /// <summary>Divide el total en aportes de distintos tamaños (múltiplos de $ 5.000) que suman exactamente el total.</summary>
    private static List<decimal> Repartir(decimal total, Random azar)
    {
        var aportes = new List<decimal>();
        var resto = total;
        while (resto > 0)
        {
            var valor = Math.Min(resto, azar.Next(2, 61) * 5_000m * (azar.Next(4) == 0 ? 4 : 1));
            if (resto - valor is > 0 and < 10_000) valor = resto;
            aportes.Add(valor);
            resto -= valor;
        }
        return aportes;
    }

    private static Donacion NuevaDonacion(int esalId, int? causaId, decimal valor, List<string> donantes,
        DateTime desde, DateTime hasta, Random azar, ref int consecutivo)
    {
        var segundos = Math.Max(3600, (int)(hasta - desde).TotalSeconds);
        var fecha = desde.AddSeconds(azar.Next(0, segundos));
        consecutivo++;
        return new Donacion
        {
            EsalId = esalId,
            CausaId = causaId,
            Codigo = $"DEMO-{consecutivo:D6}",
            DonanteId = donantes[azar.Next(donantes.Count)],
            Valor = valor,
            FechaTransferencia = fecha,
            FechaReporte = fecha,
            FechaRevision = fecha.AddHours(azar.Next(2, 30)),
            MedioPago = azar.Next(3) == 0 ? "Daviplata" : "Nequi",
            Mensaje = azar.Next(3) == 0 ? Mensajes[azar.Next(Mensajes.Length)] : null,
            SoporteRuta = "demo/sin-soporte",
            Estado = EstadoDonacion.Confirmada
        };
    }

    private static IEnumerable<FundacionDemo> Fundaciones()
    {
        yield return new FundacionDemo(
            "huellitas-de-funza", "Huellitas de Funza", "901234561-1", "Protección animal", "Funza", "3104567812",
            "Rescatamos perros abandonados en Funza, los curamos y les buscamos un hogar para siempre.",
            """
            Huellitas nació en 2016, cuando un grupo de vecinos empezó a alimentar a los perros que vivían en la plaza de mercado. Al principio era una olla de comida y unas cobijas; hoy tenemos un refugio para 70 perros, una red de hogares de paso y un equipo de voluntarios que no descansa.

            Cada perro que llega recibe atención veterinaria, vacunas, esterilización y mucho cariño. Cuando está listo, lo presentamos en nuestras jornadas de adopción y acompañamos a la familia en sus primeros meses juntos.
            """,
            "Rescatar, rehabilitar y dar en adopción responsable a perros en situación de calle en Funza y sus alrededores.",
            "Que en 2030 Funza sea un municipio sin perros abandonados en sus calles.",
            "huellitas",
            new[]
            {
                new CausaDemo("Cirugía de cadera para Rocco",
                    "Rocco fue atropellado en la vía a Mosquera. Ya está estable, pero necesita una cirugía de cadera para volver a caminar. La cirugía, los medicamentos y la terapia cuestan $ 3.200.000.",
                    3_200_000, 2_450_000, 12, "rocco"),
                new CausaDemo("Techo nuevo para los caniles antes de las lluvias",
                    "El techo de los caniles tiene goteras y en temporada de lluvias los perros duermen mojados. Con esta causa compramos tejas, canales y la mano de obra para cambiarlo.",
                    6_000_000, 1_380_000, 40, "caniles"),
                new CausaDemo("Vacunas para 60 cachorros",
                    "Recibimos dos camadas grandes en un mes. Gracias a ustedes los 60 cachorros quedaron vacunados y desparasitados.",
                    1_800_000, 1_800_000, -20, "cachorros", Cerrada: true)
            },
            new[]
            {
                new EventoDemo(CategoriaPublicacion.Evento, "Jornada de adopción en el parque de Funza",
                    "25 perros esterilizados y vacunados buscan familia. Ven a conocerlos sin compromiso.",
                    "Salimos al parque principal con los perros que están listos para tener un hogar.\n\nTodos están esterilizados, vacunados y desparasitados. Trae tu documento si quieres iniciar el proceso ese mismo día.\n\nSi no puedes adoptar, ven a pasear a uno de ellos o trae una bolsa de concentrado.",
                    9, 10, "Parque principal de Funza", 4, "adopcion"),
                new EventoDemo(CategoriaPublicacion.Evento, "Caminata canina por la adopción responsable",
                    "Camina con tu perro 5 km por Funza. La inscripción es una bolsa de comida para el refugio.",
                    "Te esperamos con tu perro (con correa y bolsitas) para caminar juntos por la adopción responsable.\n\nHabrá hidratación para humanos y perros, premios para los disfraces más creativos y una foto de recuerdo.",
                    30, 8, "Salida desde la alcaldía de Funza", 2, "caminata"),
                new EventoDemo(CategoriaPublicacion.Logro, "¡Rocco ya se pone de pie!",
                    "Gracias a sus aportes, Rocco ya está en terapia y da sus primeros pasos.",
                    "Hace un mes Rocco no podía levantarse. Hoy, con la primera parte de la terapia pagada por ustedes, ya se pone de pie solo y mueve la cola cada vez que ve a su veterinaria.\n\nFalta poco para la cirugía completa. ¡Gracias por no soltarlo!",
                    null, 0, null, 6, "rocco-logro"),
                new EventoDemo(CategoriaPublicacion.Noticia, "Buscamos hogares de paso para diciembre",
                    "En diciembre llegan más rescates. Si puedes recibir a un perro unas semanas, te necesitamos.",
                    "La fundación cubre la comida y la atención veterinaria; tú pones el cariño y un rincón de tu casa.\n\nEscríbenos por WhatsApp y te explicamos todo el proceso.",
                    null, 0, null, 12, "hogar-paso")
            },
            new[]
            {
                new NecesidadDemo("Concentrado para perro adulto", CategoriaEspecie.Alimento, "bultos de 25 kg", 30, "Cualquier marca. Comen 2 bultos por semana.", true),
                new NecesidadDemo("Cobijas", CategoriaEspecie.Accesorios, "unidades", 50, "Usadas pero limpias, para las noches frías.", false),
                new NecesidadDemo("Antipulgas", CategoriaEspecie.Medicamento, "pipetas", 70, "Para perros de 10 a 25 kg.", false)
            },
            new[]
            {
                new ProductoDemo("Collar artesanal Huellitas", "Collar tejido a mano por nuestros voluntarios, con placa para el nombre.", 25_000, "Accesorios", "p-collar"),
                new ProductoDemo("Calendario 2027 de nuestros rescatados", "12 meses con las fotos de los perros que ya encontraron familia.", 30_000, "Papelería", "p-calendario"),
                new ProductoDemo("Bandana para perro", "Tallas S, M y L. Diseños de temporada.", 15_000, "Accesorios", "p-bandana", Agotado: true)
            });

        yield return new FundacionDemo(
            "abuelos-de-oro-mosquera", "Abuelos de Oro Mosquera", "901234562-2", "Adulto mayor", "Mosquera", "3157894561",
            "Acompañamos a 40 abuelos de Mosquera con comida, salud, compañía y actividades cada semana.",
            """
            Abuelos de Oro empezó en la casa de doña Inés, que cada domingo invitaba a almorzar a los adultos mayores que vivían solos en su barrio. Hoy tenemos un centro día donde 40 abuelos reciben almuerzo, controles de salud, terapia física y, sobre todo, compañía.

            Muchos de nuestros abuelos no tienen familia cerca. Aquí celebramos sus cumpleaños, tejemos, jugamos bingo y aprendemos a usar el celular para hablar con sus nietos.
            """,
            "Mejorar la calidad de vida de los adultos mayores de Mosquera que viven solos o en situación de pobreza.",
            "Ningún abuelo de Mosquera solo, sin comida o sin quien lo acompañe al médico.",
            "abuelos",
            new[]
            {
                new CausaDemo("Pañales y cremas para 40 abuelos durante 3 meses",
                    "Varios de nuestros abuelos dependen de pañales y cremas antiescaras que sus familias no pueden pagar. Con esta causa los cubrimos durante tres meses.",
                    4_500_000, 3_100_000, 18, "cuidado"),
                new CausaDemo("Silla de ruedas para doña Rosalba",
                    "Doña Rosalba tiene 84 años y ya no puede caminar distancias largas. Una silla de ruedas le permitiría volver a venir al centro día.",
                    1_200_000, 950_000, 5, "silla-ruedas")
            },
            new[]
            {
                new EventoDemo(CategoriaPublicacion.Evento, "Tarde de bingo y tamales con los abuelos",
                    "Ven a jugar bingo con nuestros abuelos. El cartón cuesta $ 5.000 y todo es para el centro día.",
                    "Una tarde para compartir: bingo, tamales, chocolate y música de antes.\n\nPuedes venir solo, con tu familia o con tus compañeros de trabajo. Los abuelos te están esperando.",
                    6, 15, "Centro día Abuelos de Oro, Mosquera", 3, "bingo"),
                new EventoDemo(CategoriaPublicacion.Evento, "Brigada de salud visual gratuita",
                    "Una óptica aliada revisará la vista de los abuelos y entregará gafas a quienes las necesiten.",
                    "Gracias a una óptica aliada, nuestros abuelos tendrán examen de la vista y gafas sin costo.\n\nNecesitamos voluntarios para acompañarlos y organizar los turnos. Escríbenos si puedes venir.",
                    20, 9, "Centro día Abuelos de Oro, Mosquera", 1, "salud-visual"),
                new EventoDemo(CategoriaPublicacion.Logro, "Celebramos 25 cumpleaños este año",
                    "Para muchos abuelos fue la primera vez en años que alguien les cantó el cumpleaños.",
                    "Cada mes celebramos a los cumpleañeros con torta, música y fotos. Este año ya van 25 celebraciones.\n\nGracias a los voluntarios y donantes que hacen posible estos momentos.",
                    null, 0, null, 10, "cumpleanos")
            },
            new[]
            {
                new NecesidadDemo("Pañales para adulto talla M y L", CategoriaEspecie.Aseo, "paquetes", 60, "Son los que más se gastan cada semana.", true),
                new NecesidadDemo("Leche y avena", CategoriaEspecie.Alimento, "unidades", 80, "Para las onces de los abuelos.", false),
                new NecesidadDemo("Lana para tejer", CategoriaEspecie.Otro, "madejas", 40, "Para el taller de tejido de los jueves.", false)
            },
            new[]
            {
                new ProductoDemo("Bufanda tejida por los abuelos", "Cada bufanda es única y la tejió uno de nuestros abuelos en el taller de los jueves.", 40_000, "Tejidos", "p-bufanda"),
                new ProductoDemo("Mermelada casera de mora", "Frasco de 250 g preparado en el taller de cocina.", 12_000, "Alimentos", "p-mermelada")
            });

        yield return new FundacionDemo(
            "comedor-pan-y-esperanza", "Comedor Pan y Esperanza", "901234563-3", "Alimentación y comedores comunitarios", "Madrid", "3201239876",
            "Servimos 150 almuerzos al día a niños, abuelos y familias de Madrid que no tienen qué comer.",
            """
            Todo empezó en la pandemia, cuando varias madres del barrio juntaron lo que tenían para preparar una olla comunitaria. Lo que iba a durar unas semanas se volvió un comedor que hoy sirve 150 almuerzos al día, de lunes a sábado.

            La comida la cocinan voluntarias del barrio con alimentos donados por la comunidad, comerciantes y agricultores de la Sabana. Además del plato de comida, ofrecemos un espacio seguro donde los niños hacen tareas mientras sus papás trabajan.
            """,
            "Garantizar un plato de comida caliente y nutritiva a las familias más vulnerables de Madrid, Cundinamarca.",
            "Que ningún niño de Madrid se acueste con hambre.",
            "comedor",
            new[]
            {
                new CausaDemo("Mercados navideños para 80 familias",
                    "En diciembre el comedor cierra dos semanas. Queremos que 80 familias reciban un mercado completo para pasar las fiestas.",
                    8_000_000, 2_900_000, 55, "mercados"),
                new CausaDemo("Estufa industrial nueva",
                    "Nuestra estufa tiene 15 años y dos quemadores dañados. Una estufa industrial nos permitiría cocinar más rápido y de forma segura.",
                    3_500_000, 3_150_000, 9, "estufa")
            },
            new[]
            {
                new EventoDemo(CategoriaPublicacion.Evento, "Olla comunitaria navideña",
                    "Cocinamos juntos una gran olla de sancocho para 300 personas. ¡Trae un ingrediente!",
                    "Cada año celebramos la Navidad con una olla comunitaria gigante. Puedes ayudar trayendo un ingrediente, cocinando con nosotros o sirviendo.\n\nHabrá novena, villancicos y regalos para los niños.",
                    48, 11, "Salón comunal barrio Las Villas, Madrid", 2, "olla"),
                new EventoDemo(CategoriaPublicacion.Evento, "Voluntariado en la cocina: un sábado con nosotros",
                    "Pica, cocina y sirve con las voluntarias del comedor. No necesitas experiencia.",
                    "Te esperamos a las 7:00 a. m. con gorro y delantal (si no tienes, te prestamos). Cocinamos, servimos y almorzamos juntos.\n\nCupos para 10 voluntarios cada sábado.",
                    13, 7, "Comedor Pan y Esperanza, Madrid", 5, "cocina-voluntarios"),
                new EventoDemo(CategoriaPublicacion.Noticia, "Una huerta en el patio del comedor",
                    "Con semillas donadas sembramos cilantro, cebolla, lechuga y papa para la cocina.",
                    "Gracias a un grupo de agricultores de la Sabana ahora tenemos una huerta en el patio. Los niños aprenden a sembrar y lo que cosechamos va directo a la olla.",
                    null, 0, null, 14, "huerta")
            },
            new[]
            {
                new NecesidadDemo("Arroz", CategoriaEspecie.Alimento, "libras", 300, "Se gastan 20 libras al día.", true),
                new NecesidadDemo("Aceite", CategoriaEspecie.Alimento, "litros", 60, "", false),
                new NecesidadDemo("Platos y cucharas reutilizables", CategoriaEspecie.Otro, "unidades", 100, "Para dejar de usar desechables.", false)
            },
            Array.Empty<ProductoDemo>());

        yield return new FundacionDemo(
            "semillas-de-sabana", "Semillas de Sabana", "901234564-4", "Niñez y juventud", "Facatativá", "3009871234",
            "Refuerzo escolar, deporte y arte para 120 niños y jóvenes de Facatativá después del colegio.",
            """
            Semillas de Sabana nació en 2019 en un salón comunal de Facatativá, con dos profesoras jubiladas que ofrecieron ayudar con las tareas a los niños del barrio. Hoy atendemos a 120 niños y jóvenes en las tardes con refuerzo escolar, una biblioteca comunitaria, escuela de fútbol y talleres de música y pintura.

            Creemos que un niño que tiene a dónde ir después del colegio, y alguien que crea en él, tiene un futuro distinto.
            """,
            "Ofrecer a niños y jóvenes de Facatativá espacios seguros para aprender, jugar y descubrir sus talentos.",
            "Ser la segunda casa de los niños de Facatativá y acompañarlos hasta que terminen el colegio.",
            "semillas",
            new[]
            {
                new CausaDemo("Útiles escolares para 120 niños",
                    "Cada niño recibe un kit con cuadernos, colores, lápices y una maleta para empezar el año escolar con todo lo que necesita.",
                    5_000_000, 4_100_000, 25, "utiles"),
                new CausaDemo("Computadores para la biblioteca comunitaria",
                    "Queremos 6 computadores para que los niños hagan sus tareas, aprendan a programar y busquen información.",
                    9_000_000, 1_650_000, 70, "computadores"),
                new CausaDemo("Uniformes para la escuela de fútbol",
                    "Los 45 niños de la escuela de fútbol estrenaron uniforme para el torneo municipal. ¡Gracias!",
                    2_200_000, 2_200_000, -35, "futbol", Cerrada: true)
            },
            new[]
            {
                new EventoDemo(CategoriaPublicacion.Evento, "Festival de lectura en la biblioteca",
                    "Cuentos, títeres y trueque de libros para toda la familia.",
                    "Una tarde para enamorarse de la lectura: cuentería, obra de títeres hecha por los niños y trueque de libros (trae uno y llévate otro).\n\nEntrada libre.",
                    11, 14, "Biblioteca comunitaria Semillas, Facatativá", 3, "lectura"),
                new EventoDemo(CategoriaPublicacion.Evento, "Torneo infantil de fútbol Semillas",
                    "16 equipos de niños de 8 a 12 años. Ven a apoyarlos desde la tribuna.",
                    "Nuestro torneo anual reúne a equipos de toda la Sabana. Habrá medallas para todos los participantes y venta de comida para recoger fondos.\n\nSi tienes una empresa, puedes patrocinar un equipo.",
                    25, 8, "Cancha del barrio Manablanca, Facatativá", 1, "futbol"),
                new EventoDemo(CategoriaPublicacion.Logro, "18 jóvenes se graduaron del colegio este año",
                    "Todos empezaron con nosotros en primaria. Tres ya fueron admitidos en la universidad.",
                    "Este año se graduaron 18 de nuestros jóvenes. Algunos llegaron a Semillas con 7 años y ahora se preparan para estudiar enfermería, ingeniería y licenciatura.\n\nGracias a cada tutor voluntario que les dedicó sus tardes.",
                    null, 0, null, 8, "graduacion")
            },
            new[]
            {
                new NecesidadDemo("Libros infantiles y juveniles", CategoriaEspecie.Otro, "libros", 200, "En buen estado, para la biblioteca.", false),
                new NecesidadDemo("Balones de fútbol número 4", CategoriaEspecie.Accesorios, "unidades", 15, "", true),
                new NecesidadDemo("Refrigerios", CategoriaEspecie.Alimento, "paquetes", 120, "Galletas o jugos para las tardes.", false)
            },
            new[]
            {
                new ProductoDemo("Cuaderno ilustrado por los niños", "Portadas pintadas en nuestro taller de arte. 100 hojas cuadriculadas.", 10_000, "Papelería", "p-cuaderno"),
                new ProductoDemo("Camiseta Semillas de Sabana", "Algodón, tallas para niño y adulto.", 35_000, "Ropa", "p-camiseta")
            });

        yield return new FundacionDemo(
            "guardianes-del-agua", "Guardianes del Agua", "901234565-5", "Medio ambiente", "Facatativá", "3112345670",
            "Recuperamos el río Botello y los humedales de Facatativá sembrando árboles y limpiando sus orillas.",
            """
            Somos un grupo de jóvenes, abuelos y colegios que en 2018 decidimos no seguir viendo cómo el río Botello se llenaba de basura. Empezamos con una jornada de limpieza y hoy hemos sembrado más de 4.000 árboles nativos y sacado 12 toneladas de residuos de sus orillas.

            Además, enseñamos en los colegios a separar la basura y a cuidar el agua, porque lo que se aprende de niño se cuida toda la vida.
            """,
            "Proteger y recuperar las fuentes de agua de Facatativá con la participación de la comunidad.",
            "Un río Botello limpio, con árboles nativos en sus orillas y una comunidad que lo cuida.",
            "rio",
            new[]
            {
                new CausaDemo("1.000 árboles nativos para la ronda del río",
                    "Con cada $ 7.000 sembramos y cuidamos durante un año un árbol nativo (aliso, sauce, arrayán) en las orillas del río Botello.",
                    7_000_000, 4_600_000, 33, "arboles"),
                new CausaDemo("Puntos de reciclaje para 5 colegios",
                    "Queremos instalar puntos ecológicos en cinco colegios públicos de Facatativá y capacitar a los estudiantes para usarlos.",
                    2_500_000, 600_000, 47, "reciclaje")
            },
            new[]
            {
                new EventoDemo(CategoriaPublicacion.Evento, "Gran siembra de árboles en el río Botello",
                    "Vamos a sembrar 300 árboles nativos. Trae guantes, gorra y ganas de ensuciarte las manos.",
                    "Nos encontramos en el puente del río Botello. Nosotros ponemos los árboles y las herramientas; tú pones las manos.\n\nIdeal para ir en familia. Habrá refrigerio para todos los voluntarios.",
                    8, 8, "Puente del río Botello, Facatativá", 2, "arboles"),
                new EventoDemo(CategoriaPublicacion.Evento, "Limpieza del humedal",
                    "Jornada para sacar plástico y escombros del humedal. Te prestamos guantes y bolsas.",
                    "Cada mes limpiamos un tramo del humedal. La última vez sacamos 800 kilos de residuos.\n\nInscríbete por WhatsApp para organizar los grupos.",
                    22, 7, "Humedal, entrada por la vía a Zipacón", 4, "humedal"),
                new EventoDemo(CategoriaPublicacion.Logro, "Volvieron las garzas al río",
                    "Después de 3 años de siembras, los vecinos ya ven garzas y tinguas en las orillas.",
                    "Una de las mejores noticias del año: en el tramo que reforestamos volvieron las garzas, las tinguas y los copetones.\n\nEs la prueba de que el río se está recuperando. Gracias a cada persona que sembró con nosotros.",
                    null, 0, null, 16, "garza")
            },
            new[]
            {
                new NecesidadDemo("Guantes de trabajo", CategoriaEspecie.Accesorios, "pares", 80, "Talla M y L.", true),
                new NecesidadDemo("Bolsas de basura grandes", CategoriaEspecie.Aseo, "paquetes", 40, "Calibre grueso.", false)
            },
            Array.Empty<ProductoDemo>());
    }
}
