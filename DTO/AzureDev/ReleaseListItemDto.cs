namespace PasadorScript.DTO.AzureDev;

/// <summary>
/// Release listado en lote para el informe gerencial (artifacts y environments ya expandidos).
/// </summary>
public sealed class ReleaseListItemDto
{
    /// <summary>Nombre del proyecto de Azure DevOps del release.</summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>Identificador del release en Azure.</summary>
    public int ReleaseId { get; set; }

    /// <summary>Fecha de creacion UTC del release.</summary>
    public DateTime CreatedOn { get; set; }

    /// <summary>Identificador de pull request extraido de artifacts, si existe.</summary>
    public int? PullRequestId { get; set; }

    /// <summary>Identificador de build de artifacts cuando el PR no viene en el artifact.</summary>
    public int? BuildId { get; set; }

    /// <summary>Environments del release, incluidos los que nunca se desplegaron.</summary>
    public IReadOnlyList<ReleaseEnvironmentListItemDto> Environments { get; set; } = [];
}

/// <summary>
/// Environment de un release listado.
/// </summary>
public sealed class ReleaseEnvironmentListItemDto
{
    /// <summary>Identificador de environment de Azure, si viene en el payload del listado.</summary>
    public int Id { get; set; }

    /// <summary>Nombre del environment (stage).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Rank de definicion del environment.</summary>
    public int Rank { get; set; }

    /// <summary>Hora UTC de cola del ultimo deploy real, si hubo.</summary>
    public DateTime? QueuedOn { get; set; }

    /// <summary>Hora UTC de fin del ultimo deploy step aplicado, si hubo. No usa <c>modifiedOn</c> de un rechazo.</summary>
    public DateTime? CompletedOn { get; set; }

    /// <summary>Estado del environment en Azure (<c>succeeded</c>, <c>queued</c>, etc.).</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Aprobaciones PRE-deploy de este environment.</summary>
    public IReadOnlyList<ReleaseApprovalListItemDto> PreDeployApprovals { get; set; } = [];

    /// <summary>Aprobaciones POST-deploy de este environment.</summary>
    public IReadOnlyList<ReleaseApprovalListItemDto> PostDeployApprovals { get; set; } = [];
}

/// <summary>
/// Aprobacion PRE-deploy de un environment listado.
/// </summary>
public sealed class ReleaseApprovalListItemDto
{
    /// <summary>Rank de la aprobacion dentro del environment.</summary>
    public int Rank { get; set; }

    /// <summary>Attempt de deploy al que pertenece esta aprobacion.</summary>
    public int Attempt { get; set; }

    /// <summary>Estado de la aprobacion en Azure (<c>approved</c>, <c>pending</c>, etc.).</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Indica si la aprobacion es automatica.</summary>
    public bool IsAutomated { get; set; }

    /// <summary>Identificador de identidad Azure del aprobador requerido (usuario o grupo).</summary>
    public string? ApproverId { get; set; }

    /// <summary>Nombre para mostrar del aprobador requerido (usuario o grupo).</summary>
    public string? ApproverDisplayName { get; set; }

    /// <summary>Nombre para mostrar de quien aprobo realmente, si hubo.</summary>
    public string? ApprovedByDisplayName { get; set; }

    /// <summary>Marca UTC del ultimo cambio de aprobacion, si hubo.</summary>
    public DateTime? ModifiedOn { get; set; }
}

/// <summary>
/// Resultado del listado de releases de una definicion, con flags de truncado y error.
/// </summary>
public sealed class ReleaseListForReportResult
{
    /// <summary>Releases obtenidos (pueden ser parciales si Azure fallo a mitad de la paginacion).</summary>
    public IReadOnlyList<ReleaseListItemDto> Releases { get; init; } = [];

    /// <summary>Indica si se alcanzo el tope de paginas de seguridad.</summary>
    public bool PageLimitReached { get; init; }

    /// <summary>Indica si Azure devolvio error para esta definicion.</summary>
    public bool Failed { get; init; }
}
