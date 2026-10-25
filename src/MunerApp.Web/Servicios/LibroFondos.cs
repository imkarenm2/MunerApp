using Microsoft.EntityFrameworkCore;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;
using MunerApp.Web.Validacion;

namespace MunerApp.Web.Servicios;

/// <summary>Saldo de un fondo. <see cref="FondoId"/> es null si el fondo aún no tiene movimientos (no existe en la base).</summary>
public record ResumenFondo(
    string Clave, int? FondoId, string Nombre, TipoFondo Tipo, int? CausaId, bool Abierto, string? Descripcion,
    decimal Donaciones, int NumeroDonaciones, decimal TrasladosEntrada, decimal Gastos, decimal TrasladosSalida)
{
    public decimal Entradas => Donaciones + TrasladosEntrada;
    public decimal Salidas => Gastos + TrasladosSalida;
    public decimal Saldo => Entradas - Salidas;

    /// <summary>Qué parte de lo que entró ya se gastó (0 a 100).</summary>
    public int PorcentajeGastado => Entradas <= 0 ? 0 : (int)Math.Min(100, Math.Round(Gastos * 100 / Entradas));

    public bool SePuedeCerrar => Tipo is TipoFondo.Causa or TipoFondo.Proyecto;
}

/// <summary>Movimiento listo para mostrar. El soporte es público (evidencia de la veeduría).</summary>
public record MovimientoVista(
    int Id, string Clave, string Fondo, TipoMovimientoFondo Tipo, decimal Valor, string Concepto, DateTime Fecha,
    string? Proveedor, string? SoporteUrl, string? FondoRelacionado, DateTime FechaRegistro,
    bool Anulado, string? MotivoAnulacion)
{
    public bool EsSalida => Tipo is TipoMovimientoFondo.Gasto or TipoMovimientoFondo.TrasladoSalida;
}

public record ResultadoFondo(bool Exito, string Mensaje)
{
    public static ResultadoFondo Ok(string m) => new(true, m);
    public static ResultadoFondo Error(string m) => new(false, m);
}

/// <summary>
/// Libro de fondos: asignación, movimientos y destinación del dinero (ciclo de vida de la donación).
/// <para>Recepción: la donación confirmada. Asignación: el fondo al que va (se deduce, no se guarda: causa →
/// fondo de la causa; apadrinamiento → fondo de apadrinamientos; si no, fondo general). Movimientos: traslados
/// entre fondos. Destinación: gastos con soporte público. Veeduría: todo lo anterior es público.</para>
/// Todas las consultas usan IgnoreQueryFilters() y filtran por la fundación, así sirven igual para el panel de la
/// fundación, el donante y la veeduría pública. Los movimientos nunca se borran: se anulan con un motivo.
/// </summary>
public class LibroFondos
{
    public const string ClaveGeneral = "general";
    public const string ClaveApadrinamientos = "apadrinamientos";

    private readonly MunerAppDbContext _db;
    private readonly IAlmacenamientoArchivos _archivos;

    public LibroFondos(MunerAppDbContext db, IAlmacenamientoArchivos archivos)
    {
        _db = db;
        _archivos = archivos;
    }

    // ---------------- Claves ----------------

    /// <summary>Fondo al que queda asignada una donación.</summary>
    public static string ClaveDe(Donacion d) =>
        d.CausaId is int c ? ClaveCausa(c) : d.ApadrinamientoId is not null ? ClaveApadrinamientos : ClaveGeneral;

    public static string ClaveCausa(int causaId) => $"causa-{causaId}";

    public static string ClaveDe(Fondo f) => f.Tipo switch
    {
        TipoFondo.General => ClaveGeneral,
        TipoFondo.Apadrinamiento => ClaveApadrinamientos,
        TipoFondo.Causa => ClaveCausa(f.CausaId!.Value),
        _ => $"fondo-{f.Id}"
    };

    // ---------------- Consultas ----------------

    /// <summary>Todos los fondos de la fundación con su saldo (acumulado desde el inicio).</summary>
    public async Task<List<ResumenFondo>> ResumenAsync(int esalId)
    {
        var donaciones = await _db.Donaciones.IgnoreQueryFilters().AsNoTracking()
            .Where(d => d.EsalId == esalId && d.Estado == EstadoDonacion.Confirmada)
            .GroupBy(d => new { d.CausaId, d.ApadrinamientoId })
            .Select(g => new { g.Key.CausaId, Apadrinamiento = g.Key.ApadrinamientoId != null, Valor = g.Sum(d => d.Valor), N = g.Count() })
            .ToListAsync();

        var fondos = await _db.Fondos.IgnoreQueryFilters().AsNoTracking().Where(f => f.EsalId == esalId).ToListAsync();

        var movimientos = await _db.MovimientosFondo.IgnoreQueryFilters().AsNoTracking()
            .Where(m => m.EsalId == esalId && !m.Anulado)
            .GroupBy(m => new { m.FondoId, m.Tipo })
            .Select(g => new { g.Key.FondoId, g.Key.Tipo, Valor = g.Sum(m => m.Valor) })
            .ToListAsync();

        var publicos = Causa.EstadosPublicos;
        var causas = await _db.Causas.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.EsalId == esalId && publicos.Contains(c.Estado))
            .OrderByDescending(c => c.FechaCreacion)
            .Select(c => new { c.Id, c.Titulo })
            .ToListAsync();

        decimal Mov(int? fondoId, TipoMovimientoFondo tipo) =>
            fondoId is null ? 0 : movimientos.Where(m => m.FondoId == fondoId && m.Tipo == tipo).Sum(m => m.Valor);

        ResumenFondo Armar(string clave, Fondo? f, string nombre, TipoFondo tipo, int? causaId, decimal don, int n) =>
            new(clave, f?.Id, nombre, tipo, causaId, f?.Abierto ?? true, f?.Descripcion, don, n,
                Mov(f?.Id, TipoMovimientoFondo.TrasladoEntrada), Mov(f?.Id, TipoMovimientoFondo.Gasto), Mov(f?.Id, TipoMovimientoFondo.TrasladoSalida));

        var lista = new List<ResumenFondo>();

        var general = donaciones.Where(d => d.CausaId == null && !d.Apadrinamiento).ToList();
        lista.Add(Armar(ClaveGeneral, fondos.FirstOrDefault(f => f.Tipo == TipoFondo.General), "Fondo general", TipoFondo.General, null,
            general.Sum(d => d.Valor), general.Sum(d => d.N)));

        var apad = donaciones.Where(d => d.CausaId == null && d.Apadrinamiento).ToList();
        var fondoApad = fondos.FirstOrDefault(f => f.Tipo == TipoFondo.Apadrinamiento);
        if (fondoApad is not null || apad.Count > 0)
            lista.Add(Armar(ClaveApadrinamientos, fondoApad, "Apadrinamientos", TipoFondo.Apadrinamiento, null, apad.Sum(d => d.Valor), apad.Sum(d => d.N)));

        foreach (var c in causas)
        {
            var dc = donaciones.Where(d => d.CausaId == c.Id).ToList();
            lista.Add(Armar(ClaveCausa(c.Id), fondos.FirstOrDefault(f => f.CausaId == c.Id), "Causa: " + c.Titulo, TipoFondo.Causa, c.Id,
                dc.Sum(d => d.Valor), dc.Sum(d => d.N)));
        }

        foreach (var p in fondos.Where(f => f.Tipo == TipoFondo.Proyecto).OrderBy(f => f.Estado).ThenByDescending(f => f.FechaCreacion))
            lista.Add(Armar(ClaveDe(p), p, p.Nombre, TipoFondo.Proyecto, null, 0, 0));

        return lista;
    }

    public async Task<ResumenFondo?> ResumenAsync(int esalId, string clave) =>
        (await ResumenAsync(esalId)).FirstOrDefault(f => f.Clave == clave);

    /// <summary>Movimientos (gastos y traslados), los más recientes primero. Con <paramref name="clave"/> solo los de ese fondo.</summary>
    public async Task<List<MovimientoVista>> MovimientosAsync(int esalId, string? clave = null, DateTime? desde = null, DateTime? hasta = null,
        bool soloGastos = false, int maximo = 500)
    {
        var consulta = _db.MovimientosFondo.IgnoreQueryFilters().AsNoTracking().Where(m => m.EsalId == esalId);
        if (clave is not null)
        {
            var fondo = await BuscarAsync(esalId, clave);
            if (fondo is null) return new List<MovimientoVista>();
            consulta = consulta.Where(m => m.FondoId == fondo.Id);
        }
        if (desde is not null) consulta = consulta.Where(m => m.Fecha >= desde);
        if (hasta is not null) consulta = consulta.Where(m => m.Fecha < hasta);
        if (soloGastos) consulta = consulta.Where(m => m.Tipo == TipoMovimientoFondo.Gasto);

        var filas = await consulta.OrderByDescending(m => m.Fecha).ThenByDescending(m => m.Id).Take(maximo)
            .Select(m => new
            {
                m.Id, m.Tipo, m.Valor, m.Concepto, m.Fecha, m.Proveedor, m.SoporteRuta, m.FechaRegistro, m.Anulado, m.MotivoAnulacion,
                Fondo = m.Fondo!, Relacionado = m.FondoRelacionado,
                CausaFondo = m.Fondo!.Causa != null ? m.Fondo.Causa.Titulo : null,
                CausaRelacionado = m.FondoRelacionado != null && m.FondoRelacionado.Causa != null ? m.FondoRelacionado.Causa.Titulo : null
            }).ToListAsync();

        return filas.Select(m => new MovimientoVista(m.Id, ClaveDe(m.Fondo), NombreDe(m.Fondo, m.CausaFondo), m.Tipo, m.Valor, m.Concepto, m.Fecha,
            m.Proveedor, m.SoporteRuta is null ? null : _archivos.UrlPublica(m.SoporteRuta),
            m.Relacionado is null ? null : NombreDe(m.Relacionado, m.CausaRelacionado), m.FechaRegistro, m.Anulado, m.MotivoAnulacion)).ToList();
    }

    private static string NombreDe(Fondo f, string? tituloCausa) => f.Tipo switch
    {
        TipoFondo.General => "Fondo general",
        TipoFondo.Apadrinamiento => "Apadrinamientos",
        TipoFondo.Causa => "Causa: " + (tituloCausa ?? f.Nombre),
        _ => f.Nombre
    };

    /// <summary>El fondo guardado que corresponde a la clave, o null si todavía no existe (o la clave no es válida).</summary>
    private Task<Fondo?> BuscarAsync(int esalId, string clave)
    {
        var fondos = _db.Fondos.IgnoreQueryFilters().Where(f => f.EsalId == esalId);
        if (clave == ClaveGeneral) return fondos.FirstOrDefaultAsync(f => f.Tipo == TipoFondo.General);
        if (clave == ClaveApadrinamientos) return fondos.FirstOrDefaultAsync(f => f.Tipo == TipoFondo.Apadrinamiento);
        if (clave.StartsWith("causa-") && int.TryParse(clave[6..], out var c)) return fondos.FirstOrDefaultAsync(f => f.CausaId == c);
        if (clave.StartsWith("fondo-") && int.TryParse(clave[6..], out var id)) return fondos.FirstOrDefaultAsync(f => f.Id == id && f.Tipo == TipoFondo.Proyecto);
        return Task.FromResult<Fondo?>(null);
    }

    /// <summary>Busca el fondo y, si es uno de los que existen sin movimientos (general, apadrinamientos, causa), lo crea.</summary>
    private async Task<Fondo?> ObtenerOCrearAsync(int esalId, string clave, string usuarioId)
    {
        var fondo = await BuscarAsync(esalId, clave);
        if (fondo is not null) return fondo;

        if (clave == ClaveGeneral)
            fondo = new Fondo { Tipo = TipoFondo.General, Nombre = "Fondo general", Descripcion = "Donaciones sin destino: la fundación decide en qué se usan." };
        else if (clave == ClaveApadrinamientos)
            fondo = new Fondo { Tipo = TipoFondo.Apadrinamiento, Nombre = "Apadrinamientos", Descripcion = "Aportes mensuales de los padrinos." };
        else if (clave.StartsWith("causa-") && int.TryParse(clave[6..], out var causaId))
        {
            var publicos = Causa.EstadosPublicos;
            var titulo = await _db.Causas.IgnoreQueryFilters().Where(c => c.Id == causaId && c.EsalId == esalId && publicos.Contains(c.Estado))
                .Select(c => c.Titulo).FirstOrDefaultAsync();
            if (titulo is null) return null;
            fondo = new Fondo { Tipo = TipoFondo.Causa, CausaId = causaId, Nombre = titulo.Length > 120 ? titulo[..120] : titulo };
        }
        else return null;

        fondo.EsalId = esalId;
        fondo.CreadoPorId = usuarioId;
        _db.Fondos.Add(fondo);
        await _db.SaveChangesAsync();
        return fondo;
    }

    private async Task<decimal> SaldoAsync(int esalId, string clave) => (await ResumenAsync(esalId, clave))?.Saldo ?? 0;

    // ---------------- Operaciones ----------------

    /// <summary>Crea un fondo de proyecto (empieza en cero; se llena con traslados).</summary>
    public async Task<Fondo> CrearProyectoAsync(int esalId, string nombre, string? descripcion, string usuarioId)
    {
        var fondo = new Fondo
        {
            EsalId = esalId, Tipo = TipoFondo.Proyecto, Nombre = nombre.Trim(),
            Descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim(), CreadoPorId = usuarioId
        };
        _db.Fondos.Add(fondo);
        await _db.SaveChangesAsync();
        return fondo;
    }

    /// <summary>Registra un gasto con su soporte público. No deja gastar más de lo que hay en el fondo.</summary>
    public async Task<ResultadoFondo> RegistrarGastoAsync(int esalId, string clave, decimal valor, string concepto, DateTime fecha,
        string? proveedor, IFormFile soporte, ArchivoValidado info, string usuarioId)
    {
        string? ruta = null;
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            await BloquearAsync(esalId);
            var fondo = await ObtenerOCrearAsync(esalId, clave, usuarioId);
            if (fondo is null) return ResultadoFondo.Error("Ese fondo no existe.");
            if (!fondo.Abierto) return ResultadoFondo.Error("El fondo está cerrado: ya no recibe gastos.");

            var saldo = await SaldoAsync(esalId, clave);
            if (valor > saldo)
                return ResultadoFondo.Error($"El fondo solo tiene {Formatos.Pesos(saldo)}. Si el gasto se pagó con dinero de otro fondo, primero haz un traslado.");

            await using (var stream = soporte.OpenReadStream())
                ruta = await _archivos.GuardarAsync(stream, $"esal/{esalId}/fondos", info.Extension, publico: true);

            _db.MovimientosFondo.Add(new MovimientoFondo
            {
                EsalId = esalId, FondoId = fondo.Id, Tipo = TipoMovimientoFondo.Gasto, Valor = valor,
                Concepto = concepto.Trim(), Fecha = fecha.Date, Proveedor = string.IsNullOrWhiteSpace(proveedor) ? null : proveedor.Trim(),
                SoporteRuta = ruta, RegistradoPorId = usuarioId
            });
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return ResultadoFondo.Ok($"Registraste el gasto de {Formatos.Pesos(valor)}. Ya aparece en la veeduría con su soporte.");
        }
        catch
        {
            await tx.RollbackAsync();
            if (ruta is not null) await _archivos.EliminarAsync(ruta);
            throw;
        }
    }

    /// <summary>Pasa dinero de un fondo a otro. Son dos movimientos que se anulan juntos.</summary>
    public async Task<ResultadoFondo> TrasladarAsync(int esalId, string origen, string destino, decimal valor, string concepto, DateTime fecha, string usuarioId)
    {
        if (origen == destino) return ResultadoFondo.Error("Elige un fondo de destino distinto al de origen.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        await BloquearAsync(esalId);
        var desde = await ObtenerOCrearAsync(esalId, origen, usuarioId);
        var hacia = await ObtenerOCrearAsync(esalId, destino, usuarioId);
        if (desde is null || hacia is null) return ResultadoFondo.Error("Ese fondo no existe.");
        if (!desde.Abierto || !hacia.Abierto) return ResultadoFondo.Error("Uno de los fondos está cerrado.");

        var saldo = await SaldoAsync(esalId, origen);
        if (valor > saldo) return ResultadoFondo.Error($"El fondo de origen solo tiene {Formatos.Pesos(saldo)}.");

        var salida = new MovimientoFondo
        {
            EsalId = esalId, FondoId = desde.Id, FondoRelacionadoId = hacia.Id, Tipo = TipoMovimientoFondo.TrasladoSalida,
            Valor = valor, Concepto = concepto.Trim(), Fecha = fecha.Date, RegistradoPorId = usuarioId
        };
        var entrada = new MovimientoFondo
        {
            EsalId = esalId, FondoId = hacia.Id, FondoRelacionadoId = desde.Id, Tipo = TipoMovimientoFondo.TrasladoEntrada,
            Valor = valor, Concepto = concepto.Trim(), Fecha = fecha.Date, RegistradoPorId = usuarioId
        };
        _db.MovimientosFondo.Add(salida);
        _db.MovimientosFondo.Add(entrada);
        await _db.SaveChangesAsync();
        salida.MovimientoRelacionadoId = entrada.Id;
        entrada.MovimientoRelacionadoId = salida.Id;
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return ResultadoFondo.Ok($"Trasladaste {Formatos.Pesos(valor)}.");
    }

    /// <summary>Anula un gasto o un traslado (los dos lados). Sigue visible, tachado, con el motivo.</summary>
    public async Task<ResultadoFondo> AnularAsync(int esalId, int movimientoId, string motivo, string usuarioId)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        await BloquearAsync(esalId);
        var m = await _db.MovimientosFondo.IgnoreQueryFilters().Include(x => x.Fondo)
            .FirstOrDefaultAsync(x => x.Id == movimientoId && x.EsalId == esalId);
        if (m is null) return ResultadoFondo.Error("Ese movimiento no existe.");
        if (m.Anulado) return ResultadoFondo.Error("Ese movimiento ya estaba anulado.");

        var lista = new List<MovimientoFondo> { m };
        if (m.MovimientoRelacionadoId is int rel)
        {
            var otro = await _db.MovimientosFondo.IgnoreQueryFilters().Include(x => x.Fondo).FirstOrDefaultAsync(x => x.Id == rel && x.EsalId == esalId);
            if (otro is not null && !otro.Anulado) lista.Add(otro);
        }

        // Anular la entrada de un traslado le quita dinero a ese fondo: no puede quedar en negativo
        var entrada = lista.FirstOrDefault(x => x.Tipo == TipoMovimientoFondo.TrasladoEntrada);
        if (entrada is not null)
        {
            var saldo = await SaldoAsync(esalId, ClaveDe(entrada.Fondo!));
            if (entrada.Valor > saldo)
                return ResultadoFondo.Error($"No se puede anular: el fondo {entrada.Fondo!.Nombre} ya usó parte de ese dinero (le quedan {Formatos.Pesos(saldo)}). Anula primero esos gastos.");
        }

        foreach (var x in lista)
        {
            x.Anulado = true;
            x.MotivoAnulacion = motivo.Trim();
            x.AnuladoPorId = usuarioId;
            x.FechaAnulacion = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return ResultadoFondo.Ok(lista.Count > 1 ? "Anulaste el traslado en los dos fondos." : "Anulaste el movimiento. Sigue visible, tachado, con el motivo.");
    }

    /// <summary>Cierra un fondo de causa o de proyecto. Solo con saldo en cero (lo que sobre se traslada antes).</summary>
    public async Task<ResultadoFondo> CerrarAsync(int esalId, string clave, string usuarioId)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        await BloquearAsync(esalId);
        var resumen = await ResumenAsync(esalId, clave);
        if (resumen is null) return ResultadoFondo.Error("Ese fondo no existe.");
        if (!resumen.SePuedeCerrar) return ResultadoFondo.Error("El fondo general y el de apadrinamientos no se cierran.");
        if (resumen.Saldo != 0)
            return ResultadoFondo.Error($"Al fondo le quedan {Formatos.Pesos(resumen.Saldo)}. Gástalos o trasládalos a otro fondo antes de cerrarlo.");

        if (resumen.CausaId is int causaId)
        {
            var causa = await _db.Causas.IgnoreQueryFilters().AsNoTracking().FirstAsync(c => c.Id == causaId);
            if (!causa.EstaCerrada(resumen.Donaciones, Formatos.Hoy()))
                return ResultadoFondo.Error("La causa sigue recibiendo donaciones: ciérrala primero.");
        }

        var fondo = await ObtenerOCrearAsync(esalId, clave, usuarioId);
        if (fondo is null) return ResultadoFondo.Error("Ese fondo no existe.");
        if (!fondo.Abierto) return ResultadoFondo.Error("El fondo ya estaba cerrado.");
        fondo.Estado = EstadoFondo.Cerrado;
        fondo.FechaCierre = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return ResultadoFondo.Ok($"Cerraste {resumen.Nombre}.");
    }

    /// <summary>
    /// Un movimiento a la vez por fundación: así dos personas no gastan el mismo saldo al tiempo.
    /// El bloqueo se suelta solo al terminar la transacción.
    /// </summary>
    private Task BloquearAsync(int esalId) =>
        _db.Database.ExecuteSqlRawAsync(
            "EXEC sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000",
            $"munerapp-fondos-{esalId}");
}
