using PasadorScript.DTO.Auditoria;
using PasadorScript.DTO.AzureDev;
using PasadorScript.Models.Reports;

namespace PasadorScript.Services.Azure
{
    public interface IAzureReleaseService
    {
        Task<List<ReleaseWithPullRequestInfo>> GetPendingReleasesWithPullRequestInfo(int definitionId, string? statusFilter);
        Task<ReleaseDto?> ObtenerReleasePendientesAsync(int definitionId);
        Task<ReleaseDto?> ObtenerReleaseDesdePullRequestAsync(int prId);
        Task<ReleaseDto?> ObtenerReleaseDesdePullRequestAsync(string projectName, int prId);
        Task<ReleaseDto?> ObtenerReleaseAsync(int releaseId);
        Task<ReleaseAuditDto> ObtenerAuditoriaReleaseAsync(int releaseId, int pullRequestId);
        Task<List<ReleaseDefinitionInfo>> GetReleaseDefinitionsAsync();
        Task<List<ReleaseDefinitionInfo>> GetReleaseDefinitionsAsync(string repositoryId, string repositoryName);
        Task<List<EnvironmentDto>> GetReleaseDefinitionEnvironmentsAsync(int releaseDefinitionId);
        Task<ReleaseEnvironmentActionResult> ActualizarEstadoEnvironmentAsync(int releaseId, int environmentId,
            ReleaseEnvironmentAction action, string comentario = "");
        Task<ReleaseEnvironmentActionResult> ActualizarEstadoEnvironmentAsync(int approvalId,
            ReleaseEnvironmentAction action, string comentario = "", bool useSystemPat = false);
        Task<string?> DownloadReleaseLogsAsync(int releaseId, string taskName = "");
        Task<bool> RedeployEnvironmentAsync(int releaseId, int environmentId, string? comment = null);
        Task<bool> CancelarEnvironmentAsync(int releaseId, int environmentId, string? comment = null, bool useSystemPat = false);
        Task<bool> AbandonarReleaseAsync(int releaseId, string? comment = null, bool useSystemPat = false);
        Task<int?> ObtenerDeploymentPorEnvironmentAsync(int releaseId, string environmentName, bool soloActivos = true);
        Task<ReleaseTasksAndLogsDto?> GetReleaseTasksAndLogsAsync(int releaseId, int environmentId);
        Task<string?> GetReleaseTaskLogAsync(int releaseId, int environmentId, int taskId);
        Task<PRReleaseStatus> ObtenerEstadoReleasePorPRCompletadoAsync(int prId);
        Task<PullRequestAuditInfo> ObtenerPullRequestAuditInfoAsync(int pullRequestId);
        Task<List<ReleaseDefinition>> GetReleaseDefinitions();
        Task<List<string>> GetDefinitionEnvironments(string projectName, int definitionId);
        Task<List<ReleaseViewModel>> GetReleasesReport(string projectName, int definitionId, DateTime? desdeUtc, DateTime? hastaUtc, string stageFilter);
        /// <summary>
        /// Lista releases de una definicion desde <paramref name="minCreatedUtc"/> en adelante, siguiendo continuation tokens.
        /// No hace un GET por release.
        /// </summary>
        /// <param name="projectName">Nombre del proyecto de Azure DevOps.</param>
        /// <param name="definitionId">Identificador de la definicion de release.</param>
        /// <param name="minCreatedUtc">Limite inferior UTC inclusive de <c>createdOn</c>.</param>
        /// <returns>Los releases listados y flags de truncado o error de Azure.</returns>
        Task<ReleaseListForReportResult> ListReleasesForReportAsync(string projectName, int definitionId, DateTime minCreatedUtc);

        /// <summary>
        /// Completa aprobaciones PRE/POST en releases listados que vinieron sin arrays anidados
        /// (tipico de GeneXus/DLYA). Los que ya tienen aprobaciones anidadas (Scripts) no se modifican.
        /// </summary>
        /// <param name="releases">Releases cruzados con envios locales (pueden traer duplicados).</param>
        /// <returns>Task que termina cuando termina la hidratacion.</returns>
        Task HydrateMissingApprovalsForReportAsync(IReadOnlyCollection<ReleaseListItemDto> releases);
        Task<bool> HasDeployedInAnyStageAsync(int pullRequestId);
        Task<bool> HasDeployedInProdAsync(int pullRequestId);
    }
}
