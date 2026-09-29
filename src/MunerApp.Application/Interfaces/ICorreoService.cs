namespace MunerApp.Application.Interfaces;

public interface ICorreoService
{
    Task EnviarAsync(string para, string asunto, string cuerpoHtml);
}
