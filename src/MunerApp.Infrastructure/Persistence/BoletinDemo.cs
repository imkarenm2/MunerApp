using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;

namespace MunerApp.Infrastructure.Persistence;

/// <summary>
/// Publicaciones de demostración para el boletín (HU-028), para que en desarrollo y en las demos se vea lleno.
/// Son ficticias: solo se crean con Seed:BoletinDemo = true (appsettings.Development.json), nunca en producción,
/// y solo en fundaciones activas que todavía no tienen ninguna publicación. Las fechas de los eventos se calculan
/// desde hoy, así siempre hay eventos próximos.
/// </summary>
public static class BoletinDemo
{
    private static readonly TimeSpan DiferenciaColombia = TimeSpan.FromHours(5); // Colombia es UTC-5 todo el año

    private record Plantilla(CategoriaPublicacion Categoria, string Titulo, string Resumen, string Contenido,
        DateTime? FechaEventoLocal, string? Lugar, int DiasDesdePublicacion, string Emoji, string Color1, string Color2);

    public static async Task SembrarAsync(MunerAppDbContext db, IAlmacenamientoArchivos archivos)
    {
        var conPublicaciones = await db.Publicaciones.IgnoreQueryFilters().Select(p => p.EsalId).Distinct().ToListAsync();
        var esales = await db.Esales.Where(e => e.Activa && e.Slug != null && !conPublicaciones.Contains(e.Id)).ToListAsync();
        if (esales.Count == 0) return;

        var ahora = DateTime.UtcNow;
        foreach (var esal in esales)
        {
            foreach (var p in Plantillas(esal.Nombre, esal.Ciudad, Hoy()))
            {
                db.Publicaciones.Add(new Publicacion
                {
                    EsalId = esal.Id,
                    Titulo = p.Titulo,
                    Resumen = p.Resumen,
                    Contenido = p.Contenido,
                    Categoria = p.Categoria,
                    Estado = EstadoPublicacion.Publicada,
                    FechaEvento = p.FechaEventoLocal is DateTime f ? f + DiferenciaColombia : null,
                    LugarEvento = p.Lugar,
                    FechaCreacion = ahora.AddDays(-p.DiasDesdePublicacion),
                    FechaPublicacion = ahora.AddDays(-p.DiasDesdePublicacion),
                    ImagenRuta = await GuardarImagenAsync(archivos, esal.Id, p)
                });
            }
        }
        await db.SaveChangesAsync();
    }

    private static DateTime Hoy() => (DateTime.UtcNow - DiferenciaColombia).Date;

    /// <summary>El sábado siguiente a la fecha (o la misma si ya es sábado), a la hora indicada.</summary>
    private static DateTime Sabado(DateTime dia, int hora) =>
        dia.AddDays(((int)DayOfWeek.Saturday - (int)dia.DayOfWeek + 7) % 7).AddHours(hora);

    private static IEnumerable<Plantilla> Plantillas(string fundacion, string? ciudad, DateTime hoy)
    {
        var enCiudad = string.IsNullOrWhiteSpace(ciudad) ? "" : $", {ciudad}";

        // "Tu gato secreto": el sábado de mediados de diciembre más próximo
        var diciembre = new DateTime(hoy.Month == 12 && hoy.Day > 13 ? hoy.Year + 1 : hoy.Year, 12, 7);
        var gatoSecreto = Sabado(diciembre, 15);

        yield return new(CategoriaPublicacion.Evento,
            "Tu gato secreto: el amigo secreto más peludo del año",
            "Como el amigo secreto, pero en gato: te asignamos un michi rescatado, le llevas regalos y lo conoces en la gran revelación.",
            $"""
            Este diciembre jugamos al amigo secreto… ¡pero con gatos! 🐱🎁

            ¿Cómo funciona?
            1. Te inscribes escribiéndonos por la tienda o por nuestras redes antes del 30 de noviembre.
            2. Te asignamos en secreto a uno de los gatos rescatados de {fundacion}. No te decimos quién es: solo sus pistas. "Me encantan las cajas", "le tengo miedo a la aspiradora", "ronroneo como un motor".
            3. Durante las semanas previas le llevas a la sede sus regalos: comida, arena, un juguete, una cobija para el frío. Todo lo que traigas se queda para los gatos de la fundación.
            4. El día del evento hacemos la gran revelación: conoces a tu gato secreto, te tomas la foto con él y le entregas el último regalo.

            ¿Y si te enamoras? Puedes apadrinarlo o iniciar su proceso de adopción ese mismo día.

            Regalo sugerido: desde $ 30.000. Habrá chocolate caliente, buñuelos y mucho ronroneo.
            Cupos limitados: hay un participante por cada gato de la sede.
            """,
            gatoSecreto, $"Sede de {fundacion}{enCiudad}", 3, "🎁", "#C2410C", "#F59E0B");

        yield return new(CategoriaPublicacion.Evento,
            "Gran jornada de adopción: encuentra a tu compañero",
            "Más de 30 perros y gatos esterilizados y vacunados buscan familia. Ven a conocerlos sin compromiso.",
            """
            Este sábado salimos al parque con los peludos que están listos para tener un hogar.

            Todos están esterilizados, vacunados y desparasitados. Nuestro equipo te acompaña para encontrar al compañero que mejor encaje con tu familia y tu espacio.

            Si no puedes adoptar, también puedes ayudar: ven a pasear a uno de ellos durante la jornada o trae una bolsa de concentrado.

            Recuerda: adoptar es para toda la vida. Trae tu documento de identidad si quieres iniciar el proceso ese mismo día.
            """,
            Sabado(hoy.AddDays(10), 10), $"Parque principal{enCiudad}", 6, "🐾", "#0F5257", "#14B8A6");

        yield return new(CategoriaPublicacion.Evento,
            "Taller gratuito: primeros auxilios para perros y gatos",
            "Aprende qué hacer ante una intoxicación, una herida o un golpe de calor mientras llegas al veterinario.",
            """
            Una médica veterinaria voluntaria nos enseña, paso a paso, a reaccionar en las emergencias más comunes:

            • Intoxicaciones y qué NO hacer.
            • Heridas y sangrados.
            • Golpe de calor.
            • Cómo armar un botiquín básico en casa.

            El taller es gratuito y virtual. Inscríbete escribiéndonos y te enviamos el enlace. Si puedes, haz un aporte voluntario: va directo a la atención de los rescatados.
            """,
            Sabado(hoy.AddDays(24), 9), "Virtual (te enviamos el enlace al inscribirte)", 2, "🩺", "#1D4ED8", "#60A5FA");

        yield return new(CategoriaPublicacion.Evento,
            "Noche de cine y mantas por los peludos",
            "Película, crispetas y mantas al aire libre. La entrada es una donación para el refugio.",
            """
            Trae tu manta y tu mejor compañía (con o sin cuatro patas) a una noche de cine al aire libre.

            La entrada es una donación voluntaria o una bolsa de comida para perros o gatos. Habrá crispetas, chocolate y una sorpresa al final de la película.

            Todo lo recaudado se destina a los tratamientos veterinarios del mes.
            """,
            Sabado(hoy.AddDays(38), 19), $"Sede de {fundacion}{enCiudad}", 1, "🎬", "#6D28D9", "#A78BFA");

        yield return new(CategoriaPublicacion.Logro,
            "¡Superamos las 100 adopciones este año!",
            "Cien peludos ya duermen en una casa con nombre propio. Gracias a cada familia que dijo sí.",
            """
            Hoy celebramos: este año más de 100 perros y gatos rescatados encontraron familia.

            Detrás de cada adopción hay rescates de madrugada, tratamientos, hogares de paso, voluntarios que limpian, donantes que confían y familias que abren su puerta.

            Gracias por hacerlo posible. Y como todavía hay muchos esperando, vamos por los próximos cien.
            """,
            null, null, 9, "🏆", "#15803D", "#4ADE80");

        yield return new(CategoriaPublicacion.Noticia,
            "Así fue la jornada de esterilización: 48 gatitos operados",
            "Con veterinarios voluntarios esterilizamos 48 gatos de la calle en un solo día.",
            """
            El fin de semana pasado realizamos nuestra jornada de esterilización en el barrio: 48 gatos operados, desparasitados y vacunados en un solo día.

            Esterilizar es la forma más efectiva de reducir el abandono: menos camadas en la calle, menos peludos sufriendo.

            Gracias a los veterinarios voluntarios, a quienes trajeron a los gatos de su cuadra y a cada donante que aportó a esta causa.
            """,
            null, null, 15, "✂️", "#BE185D", "#F472B6");

        yield return new(CategoriaPublicacion.Evento,
            "Caminata perruna por la adopción responsable",
            "Más de 200 personas y sus perros caminaron con nosotros. ¡Gracias por venir!",
            """
            Fue una mañana inolvidable: más de 200 personas caminaron con sus perros para invitar a la adopción responsable.

            Recogimos 180 kilos de concentrado y 12 personas se inscribieron como voluntarias. ¡Nos vemos en la próxima!
            """,
            Sabado(hoy.AddDays(-20), 8), $"Parque principal{enCiudad}", 18, "🐕", "#92400E", "#D97706");

        yield return new(CategoriaPublicacion.Noticia,
            "Buscamos hogares de paso para diciembre",
            "En diciembre llegan más rescates. Si puedes recibir a un peludo unas semanas, te necesitamos.",
            """
            Cada diciembre aumentan los abandonos y el refugio se llena. Un hogar de paso le da a un perro o gato un espacio tranquilo mientras encuentra su familia definitiva.

            La fundación cubre la comida y la atención veterinaria; tú pones el cariño y un rincón de tu casa.

            Escríbenos si quieres ser hogar de paso. Te explicamos todo el proceso.
            """,
            null, null, 4, "🏡", "#0E7490", "#22D3EE");
    }

    /// <summary>Imagen sencilla (degradado con un emoji grande) para que las tarjetas se vean ilustradas.</summary>
    private static async Task<string> GuardarImagenAsync(IAlmacenamientoArchivos archivos, int esalId, Plantilla p)
    {
        var svg = $"""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1600 900" width="1600" height="900">
              <defs>
                <linearGradient id="g" x1="0" y1="0" x2="1" y2="1">
                  <stop offset="0" stop-color="{p.Color1}"/>
                  <stop offset="1" stop-color="{p.Color2}"/>
                </linearGradient>
              </defs>
              <rect width="1600" height="900" fill="url(#g)"/>
              <circle cx="1350" cy="150" r="260" fill="#ffffff" opacity=".08"/>
              <circle cx="180" cy="820" r="320" fill="#ffffff" opacity=".07"/>
              <text x="800" y="560" font-size="380" text-anchor="middle" font-family="Segoe UI Emoji, Apple Color Emoji, Noto Color Emoji, sans-serif">{WebUtility.HtmlEncode(p.Emoji)}</text>
            </svg>
            """;
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
        return await archivos.GuardarAsync(stream, $"esal/{esalId}/boletin", ".svg", publico: true);
    }
}
