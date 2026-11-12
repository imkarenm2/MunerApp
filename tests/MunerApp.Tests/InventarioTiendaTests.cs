using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;

namespace MunerApp.Tests;

/// <summary>Reglas del inventario de la tienda: cuándo se descuenta y en qué estado queda el producto.</summary>
public class InventarioTiendaTests
{
    [Fact]
    public void AlPagar_SeDescuenta()
        => Assert.Equal(-1, Pedido.EfectoEnInventario(EstadoPedido.Acordado, EstadoPedido.Pagado));

    [Fact]
    public void AlCancelarUnPedidoPagado_SeDevuelve()
        => Assert.Equal(1, Pedido.EfectoEnInventario(EstadoPedido.Pagado, EstadoPedido.Cancelado));

    [Theory]
    [InlineData(EstadoPedido.Acordado, EstadoPedido.Cancelado)] // nunca se descontó
    [InlineData(EstadoPedido.Pagado, EstadoPedido.Entregado)]   // ya se había descontado al pagar
    public void OtrosCambios_NoTocanElInventario(EstadoPedido antes, EstadoPedido despues)
        => Assert.Equal(0, Pedido.EfectoEnInventario(antes, despues));

    [Fact]
    public void CadaCambioPermitido_DescuentaComoMaximoUnaVez()
    {
        // Recorriendo todos los caminos posibles, ningún pedido descuenta dos veces ni devuelve sin haber descontado
        void Recorrer(EstadoPedido estado, int acumulado)
        {
            Assert.InRange(acumulado, -1, 0);
            foreach (var siguiente in Pedido.Siguientes(estado))
                Recorrer(siguiente, acumulado + Pedido.EfectoEnInventario(estado, siguiente));
        }
        Recorrer(EstadoPedido.Acordado, 0);
    }

    [Fact]
    public void SinUnidades_QuedaAgotado()
        => Assert.Equal(EstadoProducto.Agotado, Producto.EstadoSegunInventario(EstadoProducto.Disponible, antes: 1, despues: 0));

    [Fact]
    public void ConUnidades_SigueDisponible()
        => Assert.Equal(EstadoProducto.Disponible, Producto.EstadoSegunInventario(EstadoProducto.Disponible, antes: 5, despues: 4));

    [Fact]
    public void AgotadoPorFaltaDeUnidades_VuelveADisponibleAlReabastecer()
        => Assert.Equal(EstadoProducto.Disponible, Producto.EstadoSegunInventario(EstadoProducto.Agotado, antes: 0, despues: 10));

    [Fact]
    public void AgotadoAMano_ConUnidades_NoSeReactivaSolo()
        => Assert.Equal(EstadoProducto.Agotado, Producto.EstadoSegunInventario(EstadoProducto.Agotado, antes: 3, despues: 5));

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void ProductoOculto_SigueOculto(int despues)
        => Assert.Equal(EstadoProducto.Oculto, Producto.EstadoSegunInventario(EstadoProducto.Oculto, antes: 1, despues: despues));

    [Fact]
    public void SinCantidad_NoLlevaInventario()
    {
        Assert.False(new Producto().LlevaInventario);
        Assert.True(new Producto { Cantidad = 0 }.LlevaInventario);
    }
}
