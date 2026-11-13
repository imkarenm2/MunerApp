using System.Globalization;
using MunerApp.Application.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MunerApp.Infrastructure.Servicios;

/// <summary>
/// Reportes en PDF de la fundación con QuestPDF (misma licencia y estilo del comprobante de donación).
/// HU-040: reporte de medicamentos, en horizontal porque tiene siete columnas.
/// HU-046: rendición de cuentas de una causa.
/// HU-017 y HU-019: hoja de vida e historia clínica de un beneficiario.
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

    /// <summary>
    /// HU-046: rendición de cuentas de una causa en PDF (vertical). Es el documento que queda en la sección de
    /// transparencia: cifras de la causa, en qué se usó el dinero, fotos y la lista de soportes publicados.
    /// </summary>
    public byte[] GenerarRendicionCausa(DatosRendicionCausa d)
    {
        var publicado = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(d.FechaPublicacionUtc, DateTimeKind.Utc), ZonaColombia);
        string Pesos(decimal v) => v.ToString("C0", Co);

        return Document.Create(documento =>
        {
            documento.Page(pagina =>
            {
                pagina.Size(PageSizes.Letter);
                pagina.Margin(40);
                pagina.PageColor(Colors.White);
                pagina.DefaultTextStyle(x => x.FontSize(10).FontColor("#1F2A2E"));

                pagina.Header().Column(col =>
                {
                    col.Item().Row(fila =>
                    {
                        fila.RelativeItem().Column(c =>
                        {
                            c.Item().Text("MunerApp").FontSize(20).Bold().FontColor(Principal);
                            c.Item().Text($"{d.NombreEsal} · NIT {d.NitEsal}").FontSize(9).FontColor(TextoSuave);
                        });
                        fila.ConstantItem(200).AlignRight().Column(c =>
                        {
                            c.Item().AlignRight().Text("RENDICIÓN DE CUENTAS").FontSize(10).Bold().FontColor(Acento);
                            c.Item().AlignRight().Text(publicado.ToString("d 'de' MMMM 'de' yyyy", Co)).FontSize(9).FontColor(TextoSuave);
                        });
                    });
                    col.Item().PaddingTop(10).LineHorizontal(2).LineColor(Principal);
                });

                pagina.Content().PaddingVertical(16).Column(col =>
                {
                    col.Spacing(14);
                    col.Item().Text(d.TituloCausa).FontSize(17).Bold();
                    col.Item().Text($"{d.MotivoCierre} · fecha límite {d.FechaLimite.ToString("d 'de' MMMM 'de' yyyy", Co)}").FontSize(9).FontColor(TextoSuave);

                    col.Item().Row(fila =>
                    {
                        fila.Spacing(8);
                        void Cifra(string etiqueta, string valor, string color)
                        {
                            fila.RelativeItem().Background(PrincipalSuave).Padding(10).Column(c =>
                            {
                                c.Item().Text(valor).FontSize(13).Bold().FontColor(color);
                                c.Item().Text(etiqueta).FontSize(8).FontColor(TextoSuave);
                            });
                        }
                        Cifra("Meta", Pesos(d.Meta), Principal);
                        Cifra("Recaudado", Pesos(d.Recaudado), Principal);
                        Cifra("Donaciones confirmadas", d.Donaciones.ToString(Co), Principal);
                        if (d.ValorEjecutado is decimal ejecutado) Cifra("Valor usado", Pesos(ejecutado), Acento);
                    });

                    col.Item().Text("En qué se usó el dinero").FontSize(12).Bold().FontColor(Principal);
                    col.Item().Text(d.Texto).LineHeight(1.4f);

                    if (d.Fotos.Count > 0)
                    {
                        col.Item().Text("Fotos").FontSize(12).Bold().FontColor(Principal);
                        col.Item().Table(tabla =>
                        {
                            tabla.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
                            foreach (var foto in d.Fotos)
                                tabla.Cell().Padding(4).Height(170).AlignCenter().AlignMiddle().Image(foto).FitArea();
                        });
                    }

                    if (d.Soportes.Count > 0)
                    {
                        col.Item().Text("Soportes publicados").FontSize(12).Bold().FontColor(Principal);
                        col.Item().Column(lista =>
                        {
                            foreach (var soporte in d.Soportes)
                                lista.Item().Text($"• {soporte}").FontSize(9);
                        });
                        col.Item().Text("Los soportes se pueden descargar en la página de la causa en MunerApp.").FontSize(8).FontColor(TextoSuave);
                    }
                });

                pagina.Footer().Column(col =>
                {
                    col.Item().LineHorizontal(1).LineColor(Borde);
                    col.Item().PaddingTop(5).Row(fila =>
                    {
                        fila.RelativeItem().Text($"Publicado en MunerApp · {publicado.ToString("d 'de' MMMM 'de' yyyy, h:mm tt", Co)}").FontSize(7.5f).FontColor(TextoSuave);
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

    /// <summary>
    /// HU-017 y HU-019: hoja de vida o historia clínica de un beneficiario en PDF (vertical). Es información interna:
    /// cada página dice que es confidencial y quién la descargó.
    /// </summary>
    public byte[] GenerarFichaBeneficiario(DatosFichaBeneficiario d)
    {
        // Si la foto no se puede leer (archivo dañado), el documento sale igual, sin foto
        try { return Ficha(d); }
        catch (Exception) when (d.Foto is not null) { return Ficha(d with { Foto = null }); }
    }

    private static byte[] Ficha(DatosFichaBeneficiario d)
    {
        var generado = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(d.FechaGeneracionUtc, DateTimeKind.Utc), ZonaColombia);

        return Document.Create(documento =>
        {
            documento.Page(pagina =>
            {
                pagina.Size(PageSizes.Letter);
                pagina.Margin(40);
                pagina.PageColor(Colors.White);
                pagina.DefaultTextStyle(x => x.FontSize(9.5f).FontColor("#1F2A2E"));

                pagina.Header().Column(col =>
                {
                    col.Item().Row(fila =>
                    {
                        fila.RelativeItem().Column(c =>
                        {
                            c.Item().Text("MunerApp").FontSize(20).Bold().FontColor(Principal);
                            c.Item().Text($"{d.NombreEsal} · NIT {d.NitEsal}").FontSize(9).FontColor(TextoSuave);
                        });
                        fila.ConstantItem(220).AlignRight().Column(c =>
                        {
                            c.Item().AlignRight().Text(d.TipoDocumento.ToUpper(Co)).FontSize(10).Bold().FontColor(Acento);
                            c.Item().AlignRight().Text(d.NombreBeneficiario).FontSize(13).Bold();
                        });
                    });
                    col.Item().PaddingTop(10).LineHorizontal(2).LineColor(Principal);
                });

                pagina.Content().PaddingVertical(14).Column(col =>
                {
                    col.Spacing(14);

                    // Encabezado del beneficiario: foto interna (si hay) y resumen
                    col.Item().Row(fila =>
                    {
                        fila.Spacing(14);
                        if (d.Foto is { Length: > 0 } foto)
                            fila.ConstantItem(110).Height(110).Image(foto).FitArea();
                        fila.RelativeItem().AlignMiddle().Column(c =>
                        {
                            c.Item().Text(d.NombreBeneficiario).FontSize(17).Bold();
                            c.Item().Text(d.Resumen).FontSize(9.5f).FontColor(TextoSuave);
                        });
                    });

                    foreach (var seccion in d.Secciones.Where(x => x.Campos.Count > 0))
                    {
                        col.Item().Column(c =>
                        {
                            c.Item().PaddingBottom(4).Text(seccion.Titulo).FontSize(12).Bold().FontColor(Principal);
                            c.Item().Table(tabla =>
                            {
                                tabla.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(1.1f);
                                    cols.RelativeColumn(2f);
                                    cols.RelativeColumn(1.1f);
                                    cols.RelativeColumn(2f);
                                });

                                IContainer Celda(IContainer x) => x.BorderBottom(1).BorderColor(Borde).PaddingVertical(4).PaddingHorizontal(4);
                                var enFila = 0; // cuántos pares van en la fila actual (0 o 1)
                                foreach (var campo in seccion.Campos)
                                {
                                    // Los textos largos (observaciones, señales) ocupan toda la fila
                                    var largo = campo.Valor.Length > 55;
                                    if (largo && enFila == 1)
                                    {
                                        tabla.Cell().ColumnSpan(2).Element(Celda).Text("");
                                        enFila = 0;
                                    }
                                    tabla.Cell().Element(Celda).Text(campo.Etiqueta).FontSize(8.5f).FontColor(TextoSuave);
                                    if (largo)
                                        tabla.Cell().ColumnSpan(3).Element(Celda).Text(campo.Valor);
                                    else
                                    {
                                        tabla.Cell().Element(Celda).Text(campo.Valor);
                                        enFila = 1 - enFila;
                                    }
                                }
                                if (enFila == 1) tabla.Cell().ColumnSpan(2).Element(Celda).Text("");
                            });
                        });
                    }

                    foreach (var t in d.Tablas)
                    {
                        col.Item().Column(c =>
                        {
                            c.Item().PaddingBottom(4).Text(t.Titulo).FontSize(12).Bold().FontColor(Principal);
                            if (t.Filas.Count == 0)
                            {
                                c.Item().Border(1).BorderColor(Borde).Padding(10).Text(t.TextoSiVacia).FontColor(TextoSuave);
                                return;
                            }
                            c.Item().Table(tabla =>
                            {
                                tabla.ColumnsDefinition(cols =>
                                {
                                    foreach (var ancho in t.Anchos) cols.RelativeColumn(ancho);
                                });
                                tabla.Header(h =>
                                {
                                    foreach (var titulo in t.Columnas)
                                        h.Cell().Background(Principal).PaddingVertical(5).PaddingHorizontal(4).Text(titulo).FontSize(8.5f).Bold().FontColor(Colors.White);
                                });
                                foreach (var filaDatos in t.Filas)
                                    foreach (var valor in filaDatos)
                                        tabla.Cell().BorderBottom(1).BorderColor(Borde).PaddingVertical(4).PaddingHorizontal(4).Text(valor).FontSize(8.5f);
                            });
                        });
                    }
                });

                pagina.Footer().Column(col =>
                {
                    col.Item().LineHorizontal(1).LineColor(Borde);
                    col.Item().PaddingTop(5).Text("Documento interno y confidencial de la fundación. No lo compartas fuera de ella.").FontSize(7.5f).Bold().FontColor(Acento);
                    col.Item().Row(fila =>
                    {
                        fila.RelativeItem().Text($"Descargado de MunerApp por {d.GeneradoPor} · {generado.ToString("d 'de' MMMM 'de' yyyy, h:mm tt", Co)}").FontSize(7.5f).FontColor(TextoSuave);
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
