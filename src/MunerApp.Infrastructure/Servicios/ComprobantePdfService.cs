using System.Globalization;
using MunerApp.Application.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MunerApp.Infrastructure.Servicios;

/// <summary>
/// Comprobante de donación en PDF (HU-015) con QuestPDF, licencia Community
/// (gratuita para proyectos académicos y organizaciones con ingresos menores a 1 millón de USD).
/// </summary>
public class ComprobantePdfService : IComprobanteService
{
    private const string Principal = "#0F5257";
    private const string PrincipalSuave = "#E3F0EF";
    private const string Acento = "#C4461F";
    private const string TextoSuave = "#5B6770";
    private const string Borde = "#E4E0DA";

    private static readonly CultureInfo Co = new("es-CO");
    private static readonly TimeZoneInfo ZonaColombia = BuscarZonaColombia();

    public byte[] GenerarComprobanteDonacion(DatosComprobante d)
    {
        var confirmacion = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(d.FechaConfirmacion, DateTimeKind.Utc), ZonaColombia);

        return Document.Create(documento =>
        {
            documento.Page(pagina =>
            {
                pagina.Size(PageSizes.Letter);
                pagina.Margin(48);
                pagina.PageColor(Colors.White);
                pagina.DefaultTextStyle(x => x.FontSize(10.5f).FontColor("#1F2A2E"));

                pagina.Header().Column(col =>
                {
                    col.Item().Row(fila =>
                    {
                        fila.RelativeItem().Column(c =>
                        {
                            c.Item().Text("MunerApp").FontSize(22).Bold().FontColor(Principal);
                            c.Item().Text("Plataforma de donaciones transparentes").FontSize(9).FontColor(TextoSuave);
                        });
                        fila.ConstantItem(190).AlignRight().Column(c =>
                        {
                            c.Item().AlignRight().Text("COMPROBANTE DE DONACIÓN").FontSize(10).Bold().FontColor(Acento);
                            c.Item().AlignRight().Text(d.Codigo).FontSize(16).Bold();
                        });
                    });
                    col.Item().PaddingTop(14).LineHorizontal(2).LineColor(Principal);
                });

                pagina.Content().PaddingVertical(22).Column(col =>
                {
                    col.Spacing(16);

                    col.Item().Text(texto =>
                    {
                        texto.Span("La fundación ");
                        texto.Span(d.NombreEsal).Bold();
                        texto.Span($" (NIT {d.NitEsal}) confirma que recibió la siguiente donación de ");
                        texto.Span(d.NombreDonante).Bold();
                        texto.Span(". ¡Gracias por tu apoyo!");
                    });

                    col.Item().Background(PrincipalSuave).Padding(18).Column(c =>
                    {
                        c.Item().Text("Valor donado").FontSize(10).FontColor(TextoSuave);
                        c.Item().Text(d.Valor.ToString("C0", Co) + " COP").FontSize(26).Bold().FontColor(Principal);
                    });

                    col.Item().Table(tabla =>
                    {
                        tabla.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(170);
                            c.RelativeColumn();
                        });

                        void Fila(string etiqueta, string valor)
                        {
                            tabla.Cell().BorderBottom(1).BorderColor(Borde).PaddingVertical(7).Text(etiqueta).FontColor(TextoSuave);
                            tabla.Cell().BorderBottom(1).BorderColor(Borde).PaddingVertical(7).Text(valor).SemiBold();
                        }

                        Fila("Código de la donación", d.Codigo);
                        Fila("Donante", d.NombreDonante);
                        Fila("Correo del donante", d.CorreoDonante);
                        Fila("Fundación", d.NombreEsal);
                        Fila("NIT de la fundación", d.NitEsal);
                        Fila("Fecha de la transferencia", d.FechaTransferencia.ToString("d 'de' MMMM 'de' yyyy", Co));
                        Fila("Medio de pago", d.MedioPago);
                        if (!string.IsNullOrWhiteSpace(d.ReferenciaPago)) Fila("Referencia del pago", d.ReferenciaPago!);
                        Fila("Confirmada el", confirmacion.ToString("d 'de' MMMM 'de' yyyy, h:mm tt", Co));
                    });

                    col.Item().Border(1).BorderColor(Borde).Padding(12).Text(texto =>
                    {
                        texto.Span("Importante: ").Bold();
                        texto.Span("este comprobante certifica que la fundación verificó el ingreso del dinero en su cuenta. " +
                                   "No reemplaza el certificado de donación para efectos tributarios, que solo puede expedir la fundación.")
                             .FontColor(TextoSuave);
                    });
                });

                pagina.Footer().Column(col =>
                {
                    col.Item().LineHorizontal(1).LineColor(Borde);
                    col.Item().PaddingTop(6).Row(fila =>
                    {
                        fila.RelativeItem().Text($"Generado por MunerApp · {DateTime.UtcNow.AddHours(-5):dd/MM/yyyy HH:mm}").FontSize(8).FontColor(TextoSuave);
                        fila.ConstantItem(120).AlignRight().Text(t =>
                        {
                            t.Span("Página ").FontSize(8).FontColor(TextoSuave);
                            t.CurrentPageNumber().FontSize(8).FontColor(TextoSuave);
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
