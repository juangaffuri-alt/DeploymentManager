using PasadorScript.DbModels;
using PasadorScript.DTO.Auditoria;
using PasadorScript.DTO.AzureDev;
using PasadorScript.Extensions;
using PasadorScript.Models.Reports;
using PasadorScript.Services.Graph;
using PasadorScript.Services.Pat;
using PasadorScript.Services.User;
using PasadorScript.ViewModels;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection.Metadata.Ecma335;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Web;

namespace PasadorScript.Services.Azure
{
    public class AzureService : AzureBaseService, IAzureService
    {
        private readonly IAzureAnalyticService _azureAnalyticService;
        private readonly IAzureBranchService _azureBranchService;
        private readonly IAzureBuildService _azureBuildService;
        private readonly IAzureCommitService _azureCommitService;
        private readonly IAzureFileService _azureFileService;
        private readonly IAzurePatService _azurePatService;
        private readonly IAzurePullRequestService _azurePullRequestService;
        private readonly IAzureReleaseService _azureReleaseService;
        private readonly IAzureRepositoryService _azureRepositoryService;
        public void ForceProyectType(AzureProjectType projectType)
        {
            _azureAnalyticService.SetProjectType(projectType);
            _azureBranchService.SetProjectType(projectType);
            _azureBuildService.SetProjectType(projectType);
            _azureCommitService.SetProjectType(projectType);
            _azureFileService.SetProjectType(projectType);
            _azurePatService.SetProjectType(projectType);
            _azurePullRequestService.SetProjectType(projectType);
            _azureReleaseService.SetProjectType(projectType);
            _azureRepositoryService.SetProjectType(projectType);
        }

        public AzureService(IConfiguration configuration, IAzureAnalyticService azureAnalyticService, IAzureBranchService azureBranchService,
            IAzureBuildService azureBuildService, IAzureCommitService azureCommitService, IAzureFileService azureFileService,
            IAzurePatService azurePatService, IAzurePullRequestService azurePullRequestService, IAzureReleaseService azureReleaseService,
            IAzureRepositoryService azureRepositoryService) : base(configuration)
        {
            _azureAnalyticService = azureAnalyticService;
            _azureBranchService = azureBranchService;
            _azureBuildService = azureBuildService;
            _azureCommitService = azureCommitService;
            _azureFileService = azureFileService;
            _azurePatService = azurePatService;
            _azurePullRequestService = azurePullRequestService;
            _azureReleaseService = azureReleaseService;
            _azureRepositoryService = azureRepositoryService;
        }

        public Task AbandonarPullRequestAsync(string repositoryId, int pullRequestId) => _azurePullRequestService.AbandonarPullRequestAsync(repositoryId, pullRequestId);
        public Task<bool> AbandonarReleaseAsync(int releaseId, string? comment = null, bool useSystemPat = false) => _azureReleaseService.AbandonarReleaseAsync(releaseId, comment, useSystemPat);
        public Task<ReleaseEnvironmentActionResult> ActualizarEstadoEnvironmentAsync(int releaseId, int environmentId, ReleaseEnvironmentAction action, string comentario = "") => _azureReleaseService.ActualizarEstadoEnvironmentAsync(releaseId, environmentId, action, comentario);
        public Task<ReleaseEnvironmentActionResult> ActualizarEstadoEnvironmentAsync(int approvalId, ReleaseEnvironmentAction action, string comentario = "", bool useSystemPat = false) => _azureReleaseService.ActualizarEstadoEnvironmentAsync(approvalId, action, comentario, useSystemPat);
        public Task AddReviewerToPR(string repositoryId, int prId, params string[] reviewerDescriptors) => _azurePullRequestService.AddReviewerToPR(repositoryId, prId, reviewerDescriptors);
        public Task<BuildInfo> BuscarBuildPorCommit(string commitId, string? statusFilter, string repository = "") => _azureBuildService.BuscarBuildPorCommit(commitId, statusFilter, repository);
        public Task<bool> CancelarEnvironmentAsync(int releaseId, int environmentId, string? comment = null, bool useSystemPat = false) => _azureReleaseService.CancelarEnvironmentAsync(releaseId, environmentId, comment, useSystemPat);
        public Task<bool> CompletePullRequestAsync(string repositoryId, int pullRequestId) => _azurePullRequestService.CompletePullRequestAsync(repositoryId, pullRequestId);
        public Task<int> CreateBranchAndPullRequestAsync(string sourceBranch, string targetBranch, string repositoryId, int prIdOrig) => _azurePullRequestService.CreateBranchAndPullRequestAsync(sourceBranch, targetBranch, repositoryId, prIdOrig);
        public Task<bool> CreateBranchAsync(string repositoryId, string newBranchName) => _azureBranchService.CreateBranchAsync(repositoryId, newBranchName);
        public Task<bool> DeleteBranchAsync(string repositoryId, string branchName) => _azureBranchService.DeleteBranchAsync(repositoryId, branchName);
        public Task<string?> DownloadReleaseLogsAsync(int releaseId, string taskName = "") => _azureReleaseService.DownloadReleaseLogsAsync(releaseId, taskName);
        public Task EnableAutoCompleteForPR(string repositoryId, int prId, bool deleteSourceBranch = true, string mergeStrategy = "merge") => _azurePullRequestService.EnableAutoCompleteForPR(repositoryId, prId, deleteSourceBranch, mergeStrategy);
        public Task<int?> FindBuildDefinitionAsync(string repositoryId, string repositoryName, string branchName) => _azureBuildService.FindBuildDefinitionAsync(repositoryId, repositoryName, branchName);
        public string GenerateFileDiffView(FileDetailedDiff fileDiff) => _azurePullRequestService.GenerateFileDiffView(fileDiff);
        public string GeneratePortalLikeView(PRCompleteDataWithDiffs prData) => _azurePullRequestService.GeneratePortalLikeView(prData);
        public bool LastLabelsFailed => _azurePullRequestService.LastLabelsFailed;
        public Task<int> GeneratePullRequest(
            string repositoryId,
            string title,
            string description,
            string sourceBranch,
            string targetBranch,
            bool enableAutoComplete,
            IReadOnlyList<string>? labels = null,
            params object[] reviewers)
            => _azurePullRequestService.GeneratePullRequest(repositoryId, title, description, sourceBranch, targetBranch, enableAutoComplete, labels, reviewers);
        public Task<string> GetActualPathAzureDevOps(string repositoryId, string path) => _azureFileService.GetActualPathAzureDevOps(repositoryId, path);
        public Task<List<AgentPool>> GetAgentPoolsAsync() => _azureAnalyticService.GetAgentPoolsAsync();
        public Task<List<ArchivoConFecha>> GetArchivosEnCarpeta(string repositoryId, string branchName, string folderPath) => _azureFileService.GetArchivosEnCarpeta(repositoryId, branchName, folderPath);
        public Task<BuildInfo?> GetBuildByPullRequestAsync(int pullRequestId, int? definitionId, bool fast = false) => _azureBuildService.GetBuildByPullRequestAsync(pullRequestId, definitionId, fast);
        public Task<List<TaskInfo>> GetBuildTimelineAsync(int buildId) => _azureBuildService.GetBuildTimelineAsync(buildId);
        public Task<ICollection<CommitStatusesResponse>> GetCommitStatusesAsync(string repoId, string commitId) => _azureCommitService.GetCommitStatusesAsync(repoId, commitId);
        public Task<BuildStatusResult> GetCompleteInfoPullRequestAndBuild(int pullRequestId) => _azureBuildService.GetCompleteInfoPullRequestAndBuild(pullRequestId);
        public Task<BuildStatusResult> GetCompleteInfoPullRequestAndBuild(int pullRequestId, int definitionId) => _azureBuildService.GetCompleteInfoPullRequestAndBuild(pullRequestId, definitionId);
        public Task<BuildStatusResult> GetCompleteInfoPullRequestAndBuildFast(int pullRequestId, int definitionId) => _azureBuildService.GetCompleteInfoPullRequestAndBuildFast(pullRequestId, definitionId);
        public Task<string> GetDefaultBranch(string repoId) => _azureBranchService.GetDefaultBranch(repoId);
        public Task<int?> GetDefinitionIdByBuildId(int buildId) => _azureBuildService.GetDefinitionIdByBuildId(buildId);
        public Task<string> GetFileContentAsync(string repositoryId, string filePath, bool? desdeBranchDesa, string? branchSource) => _azureFileService.GetFileContentAsync(repositoryId, filePath, desdeBranchDesa, branchSource);
        public Task<string> GetFileContentAsync(string repositoryId, string filePath, string commitId) => _azureFileService.GetFileContentAsync(repositoryId, filePath, commitId);
        public Task<string> GetFileContentAtCommit(string repositoryId, string commitId, string filePath) => _azureCommitService.GetFileContentAtCommit(repositoryId, commitId, filePath);
        public Task<string> GetFileContentFromPathAsync(string repositoryId, string filePath) => _azureFileService.GetFileContentFromPathAsync(repositoryId, filePath);
        public Task<List<CommitHistory>> GetFileHistoryAsync(string repositoryId, string filePath, int maxResults = 50) => _azureCommitService.GetFileHistoryAsync(repositoryId, filePath, maxResults);
        public Task<List<CommitHistory>> GetFileHistoryByBranchAsync(string repositoryId, string filePath, string branch = "main", int maxResults = 50) => _azureCommitService.GetFileHistoryByBranchAsync(repositoryId, filePath, branch, maxResults);
        public Task<DateTime?> GetLastModifiedDateAsync(string repositoryId, string filePath, string branchName) => _azureFileService.GetLastModifiedDateAsync(repositoryId, filePath, branchName);
        public Task<string> GetLogContentAsync(int buildId, int logId) => _azureBuildService.GetLogContentAsync(buildId, logId);
        public Task<string> GetLogForTaskAsync(int buildId, string taskName) => _azureBuildService.GetLogForTaskAsync(buildId, taskName);
        public Task<string?> GetMergeCommitIdAsync(int prId) => _azureCommitService.GetMergeCommitIdAsync(prId);
        public Task<PipelineAnalyticsResult?> GetPipelineAnalyticsAsync(int? definitionId) => _azureAnalyticService.GetPipelineAnalyticsAsync(definitionId);
        public Task<PRCompleteData> GetPRCompleteDataAsync(int pullRequestId) =>
            _azurePullRequestService.GetPRCompleteDataAsync(pullRequestId);
        public Task<PRCompleteDataWithDiffs> GetPRCompleteDataWithDiffsAsync(int pullRequestId) => _azurePullRequestService.GetPRCompleteDataWithDiffsAsync(pullRequestId);
        public Task<AzureDevopsPullRequest?> GetPullRequestAsync(int pullRequestId) => _azurePullRequestService.GetPullRequestAsync(pullRequestId);
        public Task<AzureDevopsPullRequest?> GetPullRequestAsync(string projectName, int pullRequestId) => _azurePullRequestService.GetPullRequestAsync(projectName, pullRequestId);
        public Task<PullRequestHistory> GetPullRequestHistoryAsync(string repositoryId, int prId) => _azurePullRequestService.GetPullRequestHistoryAsync(repositoryId, prId);
        public Task<List<AzureDevopsPullRequest>> GetPullRequestsAsync(string repositoryId, string? status = null, int? top = null) => _azurePullRequestService.GetPullRequestsAsync(repositoryId, status, top);
        public Task<List<EnvironmentDto>> GetReleaseDefinitionEnvironmentsAsync(int releaseDefinitionId) =>
            _azureReleaseService.GetReleaseDefinitionEnvironmentsAsync(releaseDefinitionId);
        public Task<List<ReleaseDefinitionInfo>> GetReleaseDefinitionsAsync() => _azureReleaseService.GetReleaseDefinitionsAsync();
        public Task<List<ReleaseDefinitionInfo>> GetReleaseDefinitionsAsync(string repositoryId, string repositoryName) => _azureReleaseService.GetReleaseDefinitionsAsync(repositoryId, repositoryName);
        public Task<string?> GetReleaseTaskLogAsync(int releaseId, int environmentId, int taskId) => _azureReleaseService.GetReleaseTaskLogAsync(releaseId, environmentId, taskId);
        public Task<ReleaseTasksAndLogsDto?> GetReleaseTasksAndLogsAsync(int releaseId, int environmentId) => _azureReleaseService.GetReleaseTasksAndLogsAsync(releaseId, environmentId);
        public Task<ICollection<DTO.AzureDev.Repository>> GetRepositories() => _azureRepositoryService.GetRepositories();
        public Task<ReleaseAuditDto> ObtenerAuditoriaReleaseAsync(int releaseId, int pullRequestId) => _azureReleaseService.ObtenerAuditoriaReleaseAsync(releaseId, pullRequestId);
        public Task<int?> ObtenerDeploymentPorEnvironmentAsync(int releaseId, string environmentName, bool soloActivos = true) => _azureReleaseService.ObtenerDeploymentPorEnvironmentAsync(releaseId, environmentName, soloActivos);
        public Task<PRReleaseStatus> ObtenerEstadoReleasePorPRCompletadoAsync(int prId) => _azureReleaseService.ObtenerEstadoReleasePorPRCompletadoAsync(prId);

        public Task<PullRequestInfo?> ObtenerInfoPullRequestAsync(int prId) => _azurePullRequestService.ObtenerInfoPullRequestAsync(prId);
        public Task<ReleaseDto?> ObtenerReleaseAsync(int releaseId) => _azureReleaseService.ObtenerReleaseAsync(releaseId);
        public Task<ReleaseDto?> ObtenerReleaseDesdePullRequestAsync(int prId) => _azureReleaseService.ObtenerReleaseDesdePullRequestAsync(prId);
        public Task<ReleaseDto?> ObtenerReleaseDesdePullRequestAsync(string projectName, int prId) => _azureReleaseService.ObtenerReleaseDesdePullRequestAsync(projectName, prId);
        public Task<bool> PatProof(string pat) => _azurePatService.PatProof(pat);
        public Task<string> PushFiles(string repositoryId, string branchName, List<VirtualFileViewModel> files, bool firstCommit) => _azureFileService.PushFiles(repositoryId, branchName, files, firstCommit);
        public Task<string> PushFiles(string repositoryId, string branchName, string nameFile, string contentFile) => _azureFileService.PushFiles(repositoryId, branchName, nameFile, contentFile);
        public Task<bool> RedeployEnvironmentAsync(int releaseId, int environmentId, string? comment = null) => _azureReleaseService.RedeployEnvironmentAsync(releaseId, environmentId, comment);
        public Task<int> TriggerBuildManual(int buildDefinitionId, string owner, int pullRequestId, string sourceBranch) => _azureBuildService.TriggerBuildManual(buildDefinitionId, owner, pullRequestId, sourceBranch);
        public Task VoteOnPullRequestWithPatAsync(string pat, string AdName, string repositoryId, int pullRequestId, int voteValue, string? comments = null) => _azurePullRequestService.VoteOnPullRequestWithPatAsync(pat, AdName, repositoryId, pullRequestId, voteValue, comments);
        public Task<List<SqlStructure>> SearchSqlStructuresAsync(string repositoryId, string structureType = null, string searchTerm = null) => _azureFileService.SearchSqlStructuresAsync(repositoryId, structureType, searchTerm);

        public Task<GitCommit?> GetLastModifiedAsync(string repositoryId, string filePath) => _azureFileService.GetLastModifiedAsync(repositoryId, filePath);

        public Task<int> GetDefinitionIdByBuildName(string repositoryName) => _azureRepositoryService.GetDefinitionIdByBuildName(repositoryName);

        public Task<List<StructureInfo>> SearchStructures(string repositoryId, string structureType, string searchTerm) => _azureFileService.SearchStructures(repositoryId, structureType, searchTerm);

        public Task<PullRequestAuditInfo> ObtenerPullRequestAuditInfoAsync(int pullRequestId) => _azureReleaseService.ObtenerPullRequestAuditInfoAsync(pullRequestId);

        public Task<ReleaseDto?> ObtenerReleasePendientesAsync(int definitionId) => _azureReleaseService.ObtenerReleasePendientesAsync(definitionId);

        public Task<List<ReleaseWithPullRequestInfo>> GetPendingReleasesWithPullRequestInfo(int definitionId, string? statusFilter) => _azureReleaseService.GetPendingReleasesWithPullRequestInfo(definitionId, statusFilter);
        public Task<List<ReleaseViewModel>> GetReleasesReport(string projectName, int definitionId, DateTime? desdeUtc, DateTime? hastaUtc, string stageFilter) => _azureReleaseService.GetReleasesReport(projectName, definitionId, desdeUtc, hastaUtc, stageFilter);
        public Task<ReleaseListForReportResult> ListReleasesForReportAsync(string projectName, int definitionId, DateTime minCreatedUtc) => _azureReleaseService.ListReleasesForReportAsync(projectName, definitionId, minCreatedUtc);
        public Task HydrateMissingApprovalsForReportAsync(IReadOnlyCollection<ReleaseListItemDto> releases) => _azureReleaseService.HydrateMissingApprovalsForReportAsync(releases);
        public Task<List<ReleaseDefinition>> GetReleaseDefinitions() => _azureReleaseService.GetReleaseDefinitions();
        public Task<List<string>> GetDefinitionEnvironments(string projectName, int definitionId) => _azureReleaseService.GetDefinitionEnvironments(projectName, definitionId);
        public Task<int?> GetPrIdByBuildId(string projectName, int buildId) => _azureBuildService.GetPrIdByBuildId(projectName, buildId);
        public Task<IReadOnlyList<BuildInfo>> GetBuildsByIdsAsync(string projectName, IReadOnlyCollection<int> buildIds) => _azureBuildService.GetBuildsByIdsAsync(projectName, buildIds);

        public Task<bool> HasDeployedInAnyStageAsync(int pullRequestId) => _azureReleaseService.HasDeployedInAnyStageAsync(pullRequestId);
        public Task<bool> HasDeployedInProdAsync(int pullRequestId) => _azureReleaseService.HasDeployedInProdAsync(pullRequestId);
    }
}
