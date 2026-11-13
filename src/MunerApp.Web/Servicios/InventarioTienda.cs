using Microsoft.EntityFrameworkCore;
using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;
using MunerApp.Infrastructure.Persistence;

namespace MunerApp.Web.Servicios;

/// <summary>Cómo terminó un movimiento del inventario de la tienda.</summary>
public enum ResultadoInventario
{
    /// <summary>Se movió la cantidad.</summary>
    Hecho,

    /// <summary>El producto no lleva inventario: no se movió nada y no es un error.</summary>
    SinInventario,

    /// <summary>No hay unidades suficientes para la salida.</summary>
    Insuficiente,

    /// <summary>La entrada superaría el tope de unidades.</summary>
    SuperaMaximo,

    /// <summary>Al ajustar, la cantidad ya no era la que vio el usuario (alguien registró otro movimiento).</summary>
    Cambio
}

/// <summary>Resultado de un movimiento: cómo terminó y las unidades que quedaron (o las que había, si no se pudo).</summary>
public record MovimientoInventario(ResultadoInventario Resultado, int? Cantidad);

/// <summary>
/// Inventario de los productos de la tienda. Lo usan los productos (entradas y ajustes) y los pedidos (venta al
/// pasar a Pagado y devolución al cancelar un pedido pagado), para que la regla esté en un solo lugar.
/// Igual que el inventario de medicamentos, suma y resta de forma atómica en la base de datos: dos ventas al mismo
/// tiempo nunca dejan el producto en negativo. Debe llamarse dentro de una transacción.
/// </summary>
public class InventarioTienda
{
    /// <summary>Tope de unidades por producto, para evitar errores de digitación.</summary>
    public const int CantidadMaxima = 100_000;

    private readonly MunerAppDbContext _db;

    public InventarioTienda(MunerAppDbContext db) => _db = db;

    /// <summary>Venta (resta) por un pedido pagado.</summary>
    public Task<MovimientoInventario> VenderAsync(Producto p, int unidades, Pedido pedido, string? usuarioId)
        => MoverAsync(p, TipoMovimientoProducto.Venta, -unidades, pedido.Id, $"Pedido {pedido.Codigo}", usuarioId);

    /// <summary>Devolución (suma) porque se canceló un pedido que ya estaba pagado.</summary>
    public Task<MovimientoInventario> DevolverAsync(Producto p, int unidades, Pedido pedido, string? usuarioId)
        => MoverAsync(p, TipoMovimientoProducto.Devolucion, unidades, pedido.Id, $"Se canceló el pedido {pedido.Codigo}", usuarioId);

    /// <summary>Entrada de unidades nuevas.</summary>
    public Task<MovimientoInventario> EntradaAsync(Producto p, int unidades, string? nota, string? usuarioId)
        => MoverAsync(p, TipoMovimientoProducto.Entrada, unidades, null, nota, usuarioId);

    /// <summary>
    /// Deja la cantidad en un valor exacto (conteo, daño o pérdida). Solo cambia si la cantidad sigue siendo
    /// la que vio el usuario; si alguien vendió mientras tanto devuelve <see cref="ResultadoInventario.Cambio"/>.
    /// </summary>
    public async Task<MovimientoInventario> AjustarAsync(Producto p, int esperada, int nueva, string? nota, string? usuarioId)
    {
        if (nueva < 0 || nueva > CantidadMaxima) throw new ArgumentOutOfRangeException(nameof(nueva));
        if (nueva == esperada) return new(ResultadoInventario.Hecho, nueva);

        var filas = await _db.Productos.Where(x => x.Id == p.Id && x.Cantidad == esperada)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Cantidad, (int?)nueva));
        if (filas == 0)
            return new(ResultadoInventario.Cambio, await CantidadActualAsync(p.Id));

        await RegistrarAsync(p, TipoMovimientoProducto.Ajuste, nueva - esperada, esperada, nueva, null, nota, usuarioId);
        return new(ResultadoInventario.Hecho, nueva);
    }

    private async Task<MovimientoInventario> MoverAsync(Producto p, TipoMovimientoProducto tipo, int cambio,
        int? pedidoId, string? nota, string? usuarioId)
    {
        if (cambio == 0) throw new ArgumentOutOfRangeException(nameof(cambio));

        var actual = await CantidadActualAsync(p.Id);
        if (actual is null) return new(ResultadoInventario.SinInventario, null);

        // La condición va en el UPDATE: si otra venta se adelantó, no se actualiza ninguna fila
        var filas = cambio < 0
            ? await _db.Productos.Where(x => x.Id == p.Id && x.Cantidad != null && x.Cantidad >= -cambio)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Cantidad, x => x.Cantidad + cambio))
            : await _db.Productos.Where(x => x.Id == p.Id && x.Cantidad != null && x.Cantidad + cambio <= CantidadMaxima)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Cantidad, x => x.Cantidad + cambio));
        if (filas == 0)
        {
            var ahora = await CantidadActualAsync(p.Id);
            if (ahora is null) return new(ResultadoInventario.SinInventario, null);
            return new(cambio < 0 ? ResultadoInventario.Insuficiente : ResultadoInventario.SuperaMaximo, ahora);
        }

        var resultante = (await CantidadActualAsync(p.Id))!.Value;
        await RegistrarAsync(p, tipo, cambio, resultante - cambio, resultante, pedidoId, nota, usuarioId);
        return new(ResultadoInventario.Hecho, resultante);
    }

    private Task<int?> CantidadActualAsync(int productoId)
        => _db.Productos.AsNoTracking().Where(x => x.Id == productoId).Select(x => x.Cantidad).FirstOrDefaultAsync();

    /// <summary>Guarda el movimiento y, si hace falta, pasa el producto a Agotado o de vuelta a Disponible.</summary>
    private async Task RegistrarAsync(Producto p, TipoMovimientoProducto tipo, int cambio, int antes, int despues,
        int? pedidoId, string? nota, string? usuarioId)
    {
        var estadoActual = await _db.Productos.AsNoTracking().Where(x => x.Id == p.Id).Select(x => x.Estado).FirstAsync();
        var estadoNuevo = Producto.EstadoSegunInventario(estadoActual, antes, despues);
        if (estadoNuevo != estadoActual)
            await _db.Productos.Where(x => x.Id == p.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Estado, estadoNuevo).SetProperty(x => x.FechaActualizacion, (DateTime?)DateTime.UtcNow));

        // Si el producto está cargado en este contexto, se sincroniza sin marcarlo como modificado,
        // para que un SaveChanges posterior no sobrescriba lo que se acaba de actualizar en la base de datos
        var entrada = _db.ChangeTracker.Entries<Producto>().FirstOrDefault(e => e.Entity.Id == p.Id);
        if (entrada is not null)
        {
            entrada.Property(x => x.Cantidad).CurrentValue = despues;
            entrada.Property(x => x.Cantidad).OriginalValue = despues;
            entrada.Property(x => x.Estado).CurrentValue = estadoNuevo;
            entrada.Property(x => x.Estado).OriginalValue = estadoNuevo;
        }
        else
        {
            p.Cantidad = despues;
            p.Estado = estadoNuevo;
        }

        _db.MovimientosProducto.Add(new MovimientoProducto
        {
            EsalId = p.EsalId,
            ProductoId = p.Id,
            Tipo = tipo,
            Cantidad = cambio,
            CantidadResultante = despues,
            PedidoId = pedidoId,
            Nota = nota,
            RegistradoPorId = usuarioId
        });
        await _db.SaveChangesAsync();
    }
}
