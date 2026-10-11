using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;

namespace MunerApp.Tests;

/// <summary>HU-038 escenario 3: generación de las dosis de un tratamiento recurrente.</summary>
public class EventoAgendaTests
{
    private static readonly DateTime Inicio = new(2026, 10, 9, 13, 0, 0, DateTimeKind.Utc); // 8:00 a. m. en Colombia

    [Fact]
    public void Cada8HorasDurante5Dias_Genera15Dosis()
    {
        var fechas = EventoAgenda.FechasDeTratamiento(Inicio, TimeSpan.FromHours(8), 5);

        Assert.Equal(15, fechas.Count);
        Assert.Equal(Inicio, fechas[0]);
        Assert.Equal(Inicio.AddHours(8 * 14), fechas[^1]); // la última es antes de cumplir los 5 días
        Assert.All(fechas.Zip(fechas.Skip(1)), par => Assert.Equal(TimeSpan.FromHours(8), par.Second - par.First));
    }

    [Fact]
    public void CadaDiaDurante7Dias_Genera7Dosis()
    {
        var fechas = EventoAgenda.FechasDeTratamiento(Inicio, TimeSpan.FromDays(1), 7);
        Assert.Equal(7, fechas.Count);
        Assert.Equal(Inicio.AddDays(6), fechas[^1]);
    }

    [Fact]
    public void FrecuenciaQueNoCabeEnLaDuracion_GeneraUnaSolaDosis()
    {
        // Cada 7 días durante 3 días: solo la primera; el controlador lo rechaza como tratamiento
        var fechas = EventoAgenda.FechasDeTratamiento(Inicio, TimeSpan.FromDays(7), 3);
        Assert.Single(fechas);
    }

    [Fact]
    public void TratamientoMuyLargo_SuperaElTopeParaQueSeRechace()
    {
        // Cada hora durante 30 días serían 720 dosis: se corta en el tope + 1
        var fechas = EventoAgenda.FechasDeTratamiento(Inicio, TimeSpan.FromHours(1), 30);
        Assert.Equal(EventoAgenda.MaxDosisPorTratamiento + 1, fechas.Count);
    }

    [Theory]
    [InlineData(0, UnidadFrecuencia.Horas)]
    [InlineData(73, UnidadFrecuencia.Horas)]
    [InlineData(0, UnidadFrecuencia.Dias)]
    [InlineData(91, UnidadFrecuencia.Dias)]
    public void FrecuenciaFueraDeLimites_NoTieneIntervalo(int cada, UnidadFrecuencia unidad)
        => Assert.Null(EventoAgenda.Intervalo(cada, unidad));

    [Theory]
    [InlineData(8, UnidadFrecuencia.Horas, 8)]
    [InlineData(72, UnidadFrecuencia.Horas, 72)]
    [InlineData(2, UnidadFrecuencia.Dias, 48)]
    public void FrecuenciaValida_TieneIntervalo(int cada, UnidadFrecuencia unidad, int horas)
        => Assert.Equal(TimeSpan.FromHours(horas), EventoAgenda.Intervalo(cada, unidad));

    [Fact]
    public void SoloLosPendientesVencidosEstanAtrasados()
    {
        var ahora = Inicio.AddHours(1);
        Assert.True(new EventoAgenda { FechaProgramada = Inicio }.EstaAtrasado(ahora));
        Assert.False(new EventoAgenda { FechaProgramada = ahora.AddMinutes(1) }.EstaAtrasado(ahora));
        Assert.False(new EventoAgenda { FechaProgramada = Inicio, Estado = EstadoEventoAgenda.Realizado }.EstaAtrasado(ahora));
    }
}
