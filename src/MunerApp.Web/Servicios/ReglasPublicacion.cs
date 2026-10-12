using System.Linq.Expressions;
using MunerApp.Domain.Entities;

namespace MunerApp.Web.Servicios;

/// <summary>
/// Cuándo una fundación aparece en el directorio y en el inicio (revisión de flujos, Sprint 3):
/// debe estar activa y tener lo mínimo del perfil (descripción corta y misión).
/// Mientras tanto su página se puede abrir con el enlace, pero no se lista.
/// </summary>
public static class ReglasPublicacion
{
    public static readonly Expression<Func<Esal, bool>> EnDirectorio =
        e => e.Activa && e.Slug != null && e.DescripcionCorta != null && e.DescripcionCorta != "" && e.Mision != null && e.Mision != "";

    public static bool PerfilMinimo(Esal e) =>
        !string.IsNullOrWhiteSpace(e.DescripcionCorta) && !string.IsNullOrWhiteSpace(e.Mision);
}
