using Microsoft.AspNetCore.Mvc.ModelBinding;
using MunerApp.Application.Interfaces;
using MunerApp.Domain.Entities;
using MunerApp.Web.Areas.Fundacion.Models;
using MunerApp.Web.Validacion;

namespace MunerApp.Web.Servicios;

/// <summary>
/// Validaciones y fotos del formulario de causas. Lo usan la fundación (HU-041) y el superadministrador,
/// que también puede crear causas para una fundación.
/// </summary>
public static class FormularioCausa
{
    public const int MaxFotos = 5;
    public const long LimitePeticion = 32L * 1024 * 1024; // 5 fotos de hasta 5 MB y los demás campos
    private const decimal MetaMaxima = 1_000_000_000;

    /// <summary>Valida meta, fecha límite y justificación. Devuelve la meta ya convertida a pesos.</summary>
    public static decimal? ValidarCampos(ModelStateDictionary estado, CausaFormViewModel model, decimal recaudado)
    {
        var meta = Formatos.LeerPesos(model.Meta);
        if (!string.IsNullOrWhiteSpace(model.Meta))
        {
            if (meta is null)
                estado.AddModelError(nameof(model.Meta), "Escribe la meta solo con números, por ejemplo 2.500.000.");
            else if (meta <= 0)
                estado.AddModelError(nameof(model.Meta), "La meta debe ser mayor a cero.");
            else if (meta > MetaMaxima)
                estado.AddModelError(nameof(model.Meta), $"La meta no puede superar {Formatos.Pesos(MetaMaxima)}.");
            else if (meta <= recaudado)
                estado.AddModelError(nameof(model.Meta), $"La meta debe ser mayor a lo ya recaudado ({Formatos.Pesos(recaudado)}).");
        }

        if (model.FechaLimite is DateTime fecha)
        {
            if (fecha.Date <= DateTime.Today)
                estado.AddModelError(nameof(model.FechaLimite), "La fecha límite debe ser posterior a hoy.");
            else if (fecha.Date > DateTime.Today.AddYears(2))
                estado.AddModelError(nameof(model.FechaLimite), "La fecha límite no puede superar los 2 años.");
        }

        if (model.PideJustificacion)
        {
            var texto = model.Justificacion?.Trim() ?? "";
            if (texto.Length < 30)
                estado.AddModelError(nameof(model.Justificacion), "Cuéntale al equipo de MunerApp por qué necesitan esta causa (mínimo 30 caracteres).");
        }
        return meta;
    }

    public static async Task<List<(IFormFile Archivo, ArchivoValidado Info)>> ValidarFotosAsync(
        ModelStateDictionary estado, List<IFormFile> fotos, int fotosExistentes, bool exigirUna)
    {
        var validadas = new List<(IFormFile, ArchivoValidado)>();
        const string campo = nameof(CausaFormViewModel.Fotos);
        if (exigirUna && fotos.Count == 0)
        {
            estado.AddModelError(campo, "Agrega al menos una foto de la causa.");
            return validadas;
        }
        if (fotosExistentes + fotos.Count > MaxFotos)
        {
            estado.AddModelError(campo, $"Una causa puede tener máximo {MaxFotos} fotos" +
                (fotosExistentes > 0 ? $" (ya tiene {fotosExistentes})." : "."));
            return validadas;
        }
        foreach (var foto in fotos)
        {
            var info = await ValidadorArchivos.ValidarAsync(foto, TipoArchivo.Imagen);
            if (!info.Valido)
                estado.AddModelError(campo, $"\"{Path.GetFileName(foto.FileName)}\": {info.Error}");
            else
                validadas.Add((foto, info));
        }
        return validadas;
    }

    public static async Task AgregarFotosAsync(IAlmacenamientoArchivos archivos, Causa causa,
        List<(IFormFile Archivo, ArchivoValidado Info)> fotos, int ordenInicial)
    {
        var orden = ordenInicial;
        foreach (var (archivo, info) in fotos)
        {
            await using var stream = archivo.OpenReadStream();
            var clave = await archivos.GuardarAsync(stream, $"esal/{causa.EsalId}/causas", info.Extension, publico: true);
            causa.Fotos.Add(new FotoCausa { EsalId = causa.EsalId, Ruta = clave, Orden = orden++ });
        }
    }

    public static List<IFormFile> FotosEnviadas(CausaFormViewModel model)
        => (model.Fotos ?? new List<IFormFile>()).Where(f => f.Length > 0).ToList();
}
