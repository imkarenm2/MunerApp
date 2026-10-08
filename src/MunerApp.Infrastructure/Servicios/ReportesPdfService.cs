using System.Globalization;
using MunerApp.Application.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MunerApp.Infrastructure.Servicios;

/// <summary>
/// Reportes en PDF de la fundación con QuestPDF (misma licencia y estilo del comprobante de donación).
/// HU-040: reporte de medicamentos, en horizontal porque tiene siete columnas.
/// </summary>
public class ReportesPdfService : IReportesService
{
    private const string Principal = "#0F5257";
    private const string PrincipalSuave = "#E3F0EF";
    private const string Acento = "#C4461F";
    private const string TextoSuave = "#5B6770";
    private const string Borde = "#E4E0DA";
    private const string Error = "#C0392B";
    private const string Alerta = "#8A5A10";

    private static readonly CultureInfo Co = new("es-CO");
    private static readonly TimeZoneInfo ZonaColombia = BuscarZonaColombia();

    public byte[] GenerarReporteMedicamentos(DatosReporteMedicamentos d)
    {
        var generado = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(d.FechaGeneracionUtc, DateTimeKind.Utc), ZonaColombia);

        return Document.Create(documento =>
        {
            documento.Page(pagina =>
            {
                pagina.Size(PageSizes.Letter.Landscape());
                pagina.Margin(36);
                pagina.PageColor(Colors.White);
                pagina.DefaultTextStyle(x => x.FontSize(9).FontColor("#1F2A2E"));

                pagina.Header().Column(col =>
                {
                    col.Item().Row(fila =>
                    {
                        fila.RelativeItem().Column(c =>
                        {
                            c.Item().Text("MunerApp").FontSize(20).Bold().FontColor(Principal);
                            c.Item().Text($"{d.NombreEsal} · NIT {d.NitEsal}").FontSize(9).FontColor(TextoSuave);
                        });
                        fila.ConstantItem(260).AlignRight().Column(c =>
                        {
                            c.Item().AlignRight().Text("REPORTE DE MEDICAMENTOS E INSUMOS").FontSize(10).Bold().FontColor(Acento);
                            c.Item().AlignRight().Text(d.Filtro).FontSize(13).Bold();
                        });
                    });
                    col.Item().PaddingTop(10).LineHorizontal(2).LineColor(Principal);
                });

                pagina.Content().PaddingVertical(14).Column(col =>
                {
                    col.Spacing(12);

                    // Resumen del inventario
                    col.Item().Row(fila =>
                    {
                        fila.Spacing(10);
                        void Indicador(string etiqueta, int valor, string color)
                        {
                            fila.RelativeItem().Background(PrincipalSuave).Padding(10).Column(c =>
                            {
                                c.Item().Text(etiqueta).FontSize(8).FontColor(TextoSuave);
                                c.Item().Text(valor.ToString(Co)).FontSize(16).Bold().FontColor(color);
                            });
                        }
                        Indicador("Medicamentos en este reporte", d.Filas.Count, Principal);
                        Indicador("Vencidos", d.Vencidos, Error);
                        Indicador("Por vencer", d.PorVencer, Alerta);
                        Indicador("Stock bajo", d.StockBajo, Alerta);
                    });

                    if (d.Filas.Count == 0)
                    {
                        col.Item().Border(1).BorderColor(Borde).Padding(16).AlignCenter()
                           .Text("No hay medicamentos que coincidan con este filtro.").FontColor(TextoSuave);
                        return;
                    }

                    col.Item().Table(tabla =>
                    {
                        tabla.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2.2f); // nombre comercial
                            c.RelativeColumn(2.2f); // principio activo
                            c.RelativeColumn(1.4f); // vencimiento
                            c.RelativeColumn(2.2f); // uso
                            c.RelativeColumn(1.2f); // vía
                            c.RelativeColumn(2f);   // dosis
                            c.RelativeColumn(1.3f); // cantidad
                            c.RelativeColumn(1.2f); // estado
                        });

                        tabla.Header(h =>
                        {
                            foreach (var titulo in new[] { "Nombre comercial", "Principio activo", "Vencimiento", "Uso", "Vía", "Dosis", "Cantidad", "Estado" })
                                h.Cell().Background(Principal).PaddingVertical(6).PaddingHorizontal(5).Text(titulo).FontSize(8.5f).Bold().FontColor(Colors.White);
                        });

                        foreach (var f in d.Filas)
                        {
                            IContainer Celda(IContainer c) => c.BorderBottom(1).BorderColor(Borde).PaddingVertical(5).PaddingHorizontal(5);
                            tabla.Cell().Element(Celda).Text(f.NombreComercial).SemiBold();
                            tabla.Cell().Element(Celda).Text(f.PrincipioActivo);
                            tabla.Cell().Element(Celda).Text(f.FechaVencimiento.ToString("d MMM yyyy", Co));
                            tabla.Cell().Element(Celda).Text(f.Uso);
                            tabla.Cell().Element(Celda).Text(f.Via);
                            tabla.Cell().Element(Celda).Text(f.Dosis);
                            tabla.Cell().Element(Celda).Text(f.Cantidad);
                            var color = f.Estado.StartsWith("Vencido") ? Error : f.Estado == "Al día" ? TextoSuave : Alerta;
                            tabla.Cell().Element(Celda).Text(f.Estado).FontColor(color).SemiBold();
                        }
                    });
                });

                pagina.Footer().Column(col =>
                {
                    col.Item().LineHorizontal(1).LineColor(Borde);
                    col.Item().PaddingTop(5).Row(fila =>
                    {
                        fila.RelativeItem().Text($"Generado por MunerApp · {generado.ToString("d 'de' MMMM 'de' yyyy, h:mm tt", Co)}").FontSize(7.5f).FontColor(TextoSuave);
                        fila.ConstantItem(120).AlignRight().Text(t =>
                        {
                            t.Span("Página ").FontSize(7.5f).FontColor(TextoSuave);
                            t.CurrentPageNumber().FontSize(7.5f).FontColor(TextoSuave);
                            t.Span(" de ").FontSize(7.5f).FontColor(TextoSuave);
                            t.TotalPages().FontSize(7.5f).FontColor(TextoSuave);
                        });
                    });
                });
            });
        }).GeneratePdf();
    }

    private static TimeZoneInfo BuscarZonaColombia()
    {
        foreach (var id in new[] { "America/Bogota", "SA Pacific Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("COT", TimeSpan.FromHours(-5), "Colombia", "Colombia");
    }
}
