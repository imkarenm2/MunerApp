using MunerApp.Domain.Entities;
using MunerApp.Domain.Enums;

namespace MunerApp.Tests;

/// <summary>HU-046 escenario 1: cuándo la revisión diaria cierra una causa.</summary>
public class CierreCausasTests
{
    private static readonly DateTime Hoy = new(2026, 10, 10);

    private static Causa Causa(int venceEnDias, EstadoCausa estado = EstadoCausa.Activa) =>
        new() { Meta = 500_000, FechaLimite = Hoy.AddDays(venceEnDias), Estado = estado };

    [Fact]
    public void AlcanzoLaMeta_SeCierraPorMeta()
        => Assert.Equal(MotivoCierreCausa.MetaAlcanzada, Causa(20).MotivoDeCierre(500_000, Hoy));

    [Fact]
    public void PasoLaFechaLimite_SeCierraPorFecha()
        => Assert.Equal(MotivoCierreCausa.FechaLimite, Causa(-1).MotivoDeCierre(120_000, Hoy));

    [Fact]
    public void ElUltimoDia_SigueAbierta()
        => Assert.Null(Causa(0).MotivoDeCierre(120_000, Hoy));

    [Fact]
    public void MetaYFechaALaVez_GanaLaMeta()
        => Assert.Equal(MotivoCierreCausa.MetaAlcanzada, Causa(-3).MotivoDeCierre(600_000, Hoy));

    [Fact]
    public void UnaCausaPausadaQueVencio_TambienSeCierra()
        => Assert.Equal(MotivoCierreCausa.FechaLimite, Causa(-1, EstadoCausa.Pausada).MotivoDeCierre(0, Hoy));

    [Fact]
    public void UnaCausaYaCerrada_NoSeVuelveACerrar()
        => Assert.Null(Causa(-5, EstadoCausa.Cerrada).MotivoDeCierre(600_000, Hoy));

    [Fact]
    public void AbiertaYSinMeta_NoSeCierra()
        => Assert.Null(Causa(15).MotivoDeCierre(499_999, Hoy));
}
