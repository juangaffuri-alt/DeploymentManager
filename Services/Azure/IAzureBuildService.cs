using PasadorScript.DTO.AzureDev;

namespace PasadorScript.Services.Azure
{
    public interface IAzureBuildService
    {
        Task<BuildInfo> BuscarBuildPorCommit(string commitId, string? statusFilter, string repository = "");
        Task<List<TaskInfo>> GetBuildTimelineAsync(int buildId);
        Task<string> GetLogContentAsync(int buildId, int logId);
        Task<string> GetLogForTaskAsync(int buildId, string taskName);
        Task<BuildInfo?> GetBuildByPullRequestAsync(int pullRequestId, int? definitionId, bool fast = false);
        Task<int> TriggerBuildManual(int buildDefinitionId, string owner, int pullRequestId, string sourceBranch);
        Task<int?> FindBuildDefinitionAsync(string repositoryId, string repositoryName, string branchName);
        Task<int?> GetDefinitionIdByBuildId(int buildId);
        Task<BuildStatusResult> GetCompleteInfoPullRequestAndBuild(int pullRequestId);
        Task<BuildStatusResult> GetCompleteInfoPullRequestAndBuild(int pullRequestId, int definitionId);
        Task<BuildStatusResult> GetCompleteInfoPullRequestAndBuildFast(int pullRequestId, int definitionId);
        Task<int?> GetPrIdByBuildId(string projectName, int buildId);
        /// <summary>
        /// Obtiene builds por identificador en lotes de a lo sumo 100 IDs por request HTTP.
        /// </summary>
        /// <param name="projectName">Nombre del proyecto de Azure DevOps.</param>
        /// <param name="buildIds">Identificadores de build a resolver.</param>
        /// <returns>Los builds que Azure devolvio para los identificadores pedidos.</returns>
        Task<IReadOnlyList<BuildInfo>> GetBuildsByIdsAsync(string projectName, IReadOnlyCollection<int> buildIds);
    }
}
