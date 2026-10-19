namespace MunerApp.Domain.Enums;

/// <summary>Pregunta 12.1.3 del formato de El Reino de los Gatos: si el gato del solicitante está castrado y vacunado. Se guarda como texto.</summary>
public enum EstadoSaludGato
{
    VacunadoYEsterilizado,
    EsterilizadoSinVacunas,
    VacunadoSinEsterilizar,
    SinVacunasNiEsterilizacion
}
