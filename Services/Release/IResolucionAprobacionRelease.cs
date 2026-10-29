using PasadorScript.DTO.AzureDev;

namespace PasadorScript.Services.Release;

/// <summary>
/// Resuelve qué aprobaciones de un release puede usar el rol de la sesión.
/// </summary>
public interface IResolucionAprobacionRelease
{
    /// <summary>
    /// Clave de <c>ViewData</c> con el diccionario de stages resueltos.
    /// </summary>
    const string ViewDataKey = "AprobacionesPorStage";

    /// <summary>
    /// Comentario que va a Azure cuando la aprobación no trae texto propio.
    /// </summary>
    const string ComentarioAprobadoManualmente = "Aprobado Manualmente";

    /// <summary>
    /// Devuelve, por stage, las aprobaciones de la fase actual cuyo descriptor
    /// corresponde a la intersección entre los roles de la sesión y los roles válidos del stage.
    /// </summary>
    /// <param name="release">Release ya cargado desde Azure.</param>
    /// <param name="rolesSesion">Nombres de rol de la sesión (<c>QA</c>, <c>IYC</c>, <c>Coordinadores</c>).</param>
    IReadOnlyList<ResolucionStage> Resolver(ReleaseDto release, IReadOnlyCollection<string> rolesSesion);

    /// <summary>
    /// Busca una aprobación que siga permitida para ese usuario en la fase actual.
    /// </summary>
    /// <param name="release">Release ya cargado desde Azure.</param>
    /// <param name="rolesSesion">Nombres de rol de la sesión.</param>
    /// <param name="approvalId">Identificador de la aprobación de Azure.</param>
    /// <returns>La aprobación permitida, o <see langword="null"/> si el rol no puede usarla.</returns>
    AprobacionResuelta? BuscarPermitida(ReleaseDto release, IReadOnlyCollection<string> rolesSesion, int approvalId);
}

/// <summary>
/// Aprobación de Azure que el rol de la sesión puede aprobar o rechazar.
/// </summary>
public sealed class AprobacionResuelta
{
    public int Id { get; init; }

    public string DisplayName { get; init; } = "";

    public string Descriptor { get; init; } = "";

    public bool EsPosterior { get; init; }

    /// <summary>
    /// La aprobación posterior de QA, fuera de producción, acepta Validación Acotada.
    /// La previa solo confirma.
    /// </summary>
    public bool PermiteSinPrueba { get; init; }
}

/// <summary>
/// Aprobaciones permitidas de un stage. Sin Prueba solo entra en la fase posterior de QA.
/// </summary>
public sealed class ResolucionStage
{
    public int EnvironmentId { get; init; }

    public bool IncluyeSinPrueba { get; init; }

    public IReadOnlyList<AprobacionResuelta> Aprobaciones { get; init; } = [];
}
