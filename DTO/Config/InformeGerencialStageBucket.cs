namespace PasadorScript.DTO.Config;

/// <summary>
/// Bucket generico de ultimo stage para el informe gerencial de releases (por ejemplo QA, DF o PROD).
/// </summary>
public sealed class InformeGerencialStageBucket
{
    /// <summary>Valor de filtro enviado en el query string.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Etiqueta del combo. Si esta vacia, se usa <see cref="Key"/>.</summary>
    public string Display { get; set; } = string.Empty;

    /// <summary>Nombres de environment de Azure que pertenecen a este bucket.</summary>
    public List<string> Nombres { get; set; } = [];
}
