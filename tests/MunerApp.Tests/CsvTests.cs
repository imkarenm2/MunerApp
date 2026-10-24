using MunerApp.Application.Exportacion;

namespace MunerApp.Tests;

/// <summary>Celdas del CSV de veeduría: texto seguro para abrir en Excel.</summary>
public class CsvTests
{
    [Theory]
    [InlineData("=HYPERLINK(\"http://x\")", "\"'=HYPERLINK(\"\"http://x\"\")\"")]
    [InlineData("+57 300", "\"'+57 300\"")]
    [InlineData("-10", "\"'-10\"")]
    [InlineData("@SUMA(A1)", "\"'@SUMA(A1)\"")]
    [InlineData("\tcelda", "\"'\tcelda\"")]
    public void UnaFormula_QuedaComoTexto(string valor, string esperado)
        => Assert.Equal(esperado, ArchivoCsv.Celda(valor));

    [Fact]
    public void UnTextoNormal_SoloVaEntreComillas()
        => Assert.Equal("\"Concentrado para perro\"", ArchivoCsv.Celda("Concentrado para perro"));

    [Fact]
    public void LasComillasInternas_SeDuplican()
        => Assert.Equal("\"Bufanda \"\"de lana\"\"\"", ArchivoCsv.Celda("Bufanda \"de lana\""));

    [Fact]
    public void UnValorVacio_EsUnaCeldaVacia()
        => Assert.Equal("\"\"", ArchivoCsv.Celda(null));
}
