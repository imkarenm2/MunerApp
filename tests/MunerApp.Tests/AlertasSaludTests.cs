using MunerApp.Domain.Entities;

namespace MunerApp.Tests;

/// <summary>HU-039: reglas de las alertas de salud.</summary>
public class AlertasSaludTests
{
    private static readonly DateTime Hoy = new(2026, 10, 9);

    private static Medicamento Vence(int enDias, DateTime? avisado = null) =>
        new() { FechaVencimiento = Hoy.AddDays(enDias), VencimientoAvisado = avisado };

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(30)]
    public void PorVencerDentroDelPlazo_SinAvisoPrevio_SeAvisa(int dias)
        => Assert.True(Vence(dias).DebeAvisarVencimiento(Hoy, 30));

    [Fact]
    public void FueraDelPlazo_NoSeAvisa() => Assert.False(Vence(31).DebeAvisarVencimiento(Hoy, 30));

    [Fact]
    public void YaVencido_NoEsPorVencer() => Assert.False(Vence(-1).DebeAvisarVencimiento(Hoy, 30));

    [Fact]
    public void YaAvisadoDeEsaFecha_NoSeRepite()
        => Assert.False(Vence(10, avisado: Hoy.AddDays(10)).DebeAvisarVencimiento(Hoy, 30));

    [Fact]
    public void SiCambiaLaFechaDeVencimiento_SeVuelveAAvisar()
        => Assert.True(Vence(20, avisado: Hoy.AddDays(5)).DebeAvisarVencimiento(Hoy, 30));

    [Fact]
    public void PlazoConfigurado_CambiaQueEsPorVencer()
    {
        Assert.False(Vence(10).DebeAvisarVencimiento(Hoy, 7));
        Assert.True(Vence(10).DebeAvisarVencimiento(Hoy, 15));
    }

    [Theory]
    [InlineData(5, 3, true)]   // baja de 5 a 3 con mínimo 3: cruza
    [InlineData(5, 1, true)]   // baja de 5 a 1: cruza
    [InlineData(3, 2, false)]  // ya estaba en el mínimo: no se repite el aviso
    [InlineData(10, 4, false)] // sigue por encima del mínimo
    public void StockBajo_SoloAlCruzarElMinimo(decimal antes, decimal despues, bool avisa)
        => Assert.Equal(avisa, Medicamento.CruzoMinimo(3, antes, despues));

    [Fact]
    public void SinCantidadMinima_NoHayStockBajo() => Assert.False(Medicamento.CruzoMinimo(null, 5, 0));
}
