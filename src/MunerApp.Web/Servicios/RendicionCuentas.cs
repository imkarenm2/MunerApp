using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Validacion;

namespace MunerApp.Web.Servicios;

/// <summary>Una foto o un soporte ya validado, listo para guardarse.</summary>
public record ArchivoParaRendicion(IFormFile Archivo, ArchivoValidado Info, bool EsSoporte);

/// <summary>
/// HU-046 escenarios 2 y 3: publica la rendición de cuentas de una causa cerrada.
/// En un solo paso queda (1) en la causa, (2) en la sección de transparencia como un PDF generado, enlazado en
/// <see cref="Causa.RendicionDocumentoId"/>, y (3) en el boletín (HU-027) con la categoría Rendición de cuentas.
/// Después se avisa a quienes donaron a la causa.
/// </summary>
public class RendicionCuentas
{
    private readonly MunerAppDbContext _db;
    private readonly IAlmacenamientoArchivos _archivos;
    private readonly IReportesService _reportes;
    private readonly INotificacionService _notificaciones;
    private readonly CierreCausas _cierre;
    private readonly ILogger<RendicionCuentas> _logger;

    public RendicionCuentas(MunerAppDbContext db, IAlmacenamientoArchivos archivos, IReportesService reportes,
        INotificacionService notificaciones, CierreCausas cierre, ILogger<RendicionCuentas> logger)
    {
        _db = db;
        _archivos = archivos;
        _reportes = reportes;
        _notificaciones = notificaciones;
        _cierre = cierre;
        _logger = logger;
    }

    /// <summary>Publica la rendición. Devuelve cuántos donantes fueron avisados.</summary>
    public async Task<int> PublicarAsync(Causa causa, string texto, decimal? valorEjecutado,
        IReadOnlyList<ArchivoParaRendicion> archivos, string usuarioId)
    {
        var esal = await _db.Esales.AsNoTracking().Where(e => e.Id == causa.EsalId)
            .Select(e => new { e.Nombre, e.Nit, e.Slug }).FirstAsync();
        var confirmadas = _db.Donaciones.IgnoreQueryFilters()
            .Where(d => d.CausaId == causa.Id && d.EsalId == causa.EsalId && d.Estado == EstadoDonacion.Confirmada);
        var recaudado = await confirmadas.SumAsync(d => (decimal?)d.Valor) ?? 0;
        var donaciones = await confirmadas.CountAsync();
        var donantes = await confirmadas.Select(d => d.DonanteId).Distinct().ToListAsync();
        var hoy = Formatos.Local(DateTime.UtcNow).Date;

        // Si la revisión diaria todavía no la cerró, se cierra ahora con el motivo que corresponde
        if (causa.Estado != EstadoCausa.Cerrada)
            await _cierre.CerrarAsync(causa, causa.MotivoDeCierre(recaudado, hoy) ?? MotivoCierreCausa.Manual, recaudado, avisar: false);

        // 1. Archivos públicos (se guardan primero; si algo falla después, se borran)
        var guardados = new List<string>();
        try
        {
            var rendicion = new RendicionCausa
            {
                EsalId = causa.EsalId,
                CausaId = causa.Id,
                Texto = texto.Trim(),
                ValorEjecutado = valorEjecutado,
                Recaudado = recaudado,
                Donaciones = donaciones,
                PublicadaPorId = usuarioId
            };
            var fotos = new List<byte[]>();
            var orden = 0;
            foreach (var a in archivos.OrderBy(x => x.EsSoporte))
            {
                byte[] bytes;
                await using (var s = a.Archivo.OpenReadStream())
                using (var ms = new MemoryStream())
                {
                    await s.CopyToAsync(ms);
                    bytes = ms.ToArray();
                }
                var clave = await GuardarAsync(bytes, $"esal/{causa.EsalId}/rendiciones", a.Info.Extension, guardados);
                if (!a.EsSoporte) fotos.Add(bytes);
                rendicion.Archivos.Add(new ArchivoRendicion
                {
                    EsalId = causa.EsalId,
                    EsSoporte = a.EsSoporte,
                    Ruta = clave,
                    NombreOriginal = Recortar(Path.GetFileName(a.Archivo.FileName), 200),
                    Orden = orden++
                });
            }

            // 2. PDF para la sección de transparencia
            var pdf = _reportes.GenerarRendicionCausa(new DatosRendicionCausa(
                esal.Nombre, esal.Nit, causa.Titulo, causa.Meta, recaudado, donaciones, valorEjecutado, causa.FechaLimite,
                Domain.Constantes.Textos.De(causa.MotivoCierre!.Value), rendicion.Texto, fotos,
                rendicion.Archivos.Where(x => x.EsSoporte).Select(x => x.NombreOriginal).ToList(), DateTime.UtcNow));
            var clavePdf = await GuardarAsync(pdf, $"esal/{causa.EsalId}/documentos", ".pdf", guardados);
            var documento = new DocumentoTransparencia
            {
                EsalId = causa.EsalId,
                Titulo = Recortar($"Rendición de cuentas: {causa.Titulo}", 150),
                Categoria = CategoriaDocumento.RendicionCuentas,
                Descripcion = Recortar($"Causa \"{causa.Titulo}\": {Formatos.Pesos(recaudado)} recaudados en {donaciones} donaciones. Generado por MunerApp.", 300),
                Ruta = clavePdf,
                NombreOriginal = $"rendicion-de-cuentas-causa-{causa.Id}.pdf",
                Extension = ".pdf",
                TamanoBytes = pdf.LongLength,
                PublicadoPorId = usuarioId
            };

            // 3. Publicación en el boletín, con una copia propia de la primera foto (el boletín la puede reemplazar o borrar)
            var publicacion = new Publicacion
            {
                EsalId = causa.EsalId,
                Titulo = Recortar($"Rendición de cuentas: {causa.Titulo}", 150),
                Resumen = Recortar(rendicion.Texto, 300),
                Contenido = Recortar(ContenidoBoletin(rendicion, causa), 8000),
                Categoria = CategoriaPublicacion.RendicionCuentas,
                Estado = EstadoPublicacion.Publicada,
                FechaPublicacion = DateTime.UtcNow,
                CausaId = causa.Id,
                CreadaPorId = usuarioId
            };
            var primeraFoto = archivos.FirstOrDefault(x => !x.EsSoporte);
            if (primeraFoto is not null && fotos.Count > 0)
                publicacion.ImagenRuta = await GuardarAsync(fotos[0], $"esal/{causa.EsalId}/boletin", primeraFoto.Info.Extension, guardados);

            // Todo en una transacción: la rendición, el documento, la publicación, la causa y los avisos
            var estrategia = _db.Database.CreateExecutionStrategy();
            await estrategia.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync();
                _db.DocumentosTransparencia.Add(documento);
                _db.Publicaciones.Add(publicacion);
                await _db.SaveChangesAsync();

                rendicion.DocumentoId = documento.Id;
                rendicion.PublicacionId = publicacion.Id;
                causa.RendicionDocumentoId = documento.Id;
                _db.RendicionesCausa.Add(rendicion);

                // Escenario 3: aviso a los donantes de la causa
                foreach (var donante in donantes)
                    _notificaciones.Agregar(donante, $"Rendición de cuentas: {causa.Titulo}",
                        $"{esal.Nombre} publicó en qué usó lo recaudado en la causa que apoyaste. ¡Gracias por tu aporte!",
                        $"/fundaciones/{esal.Slug}/causas/{causa.Id}#rendicion", "bi-clipboard-data");

                await _db.SaveChangesAsync();
                await tx.CommitAsync();
            });
            return donantes.Count;
        }
        catch
        {
            foreach (var clave in guardados)
            {
                try { await _archivos.EliminarAsync(clave); }
                catch (Exception ex) { _logger.LogWarning(ex, "No se pudo borrar el archivo {Clave} de una rendición fallida", clave); }
            }
            throw;
        }
    }

    private async Task<string> GuardarAsync(byte[] contenido, string carpeta, string extension, List<string> guardados)
    {
        using var ms = new MemoryStream(contenido);
        var clave = await _archivos.GuardarAsync(ms, carpeta, extension, publico: true);
        guardados.Add(clave);
        return clave;
    }

    private static string ContenidoBoletin(RendicionCausa r, Causa c)
    {
        var cifras = $"Meta: {Formatos.Pesos(c.Meta)} · Recaudado: {Formatos.Pesos(r.Recaudado)} en {r.Donaciones} " +
                     (r.Donaciones == 1 ? "donación" : "donaciones") +
                     (r.ValorEjecutado is decimal v ? $" · Valor usado: {Formatos.Pesos(v)}" : "");
        return $"{r.Texto}\n\n{cifras}\n\nLos soportes y el documento completo están en la página de la causa y en la sección de transparencia de la fundación.";
    }

    private static string Recortar(string texto, int max) => texto.Length <= max ? texto : texto[..(max - 1)] + "…";
}
