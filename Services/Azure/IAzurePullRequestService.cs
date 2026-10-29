using PasadorScript.DTO.AzureDev;

namespace PasadorScript.Services.Azure
{
    public interface IAzurePullRequestService
    {
        /// <summary>
        /// Gets a value that indicates whether the last <see cref="GeneratePullRequest"/> call created the PR but failed to apply one or more labels.
        /// </summary>
        bool LastLabelsFailed { get; }

        /// <summary>
        /// Creates a pull request with the given title and description, then applies the supplied labels.
        /// </summary>
        /// <param name="repositoryId">The Azure DevOps repository identifier.</param>
        /// <param name="title">The pull request title. If empty after trim, a fallback based on the branch names is used.</param>
        /// <param name="description">The pull request body.</param>
        /// <param name="sourceBranch">The source branch name without <c>refs/heads/</c>.</param>
        /// <param name="targetBranch">The target branch name without <c>refs/heads/</c>.</param>
        /// <param name="enableAutoComplete"><see langword="true" /> to enable auto-complete; currently unused by this method.</param>
        /// <param name="labels">The label names to attach after creation, or <see langword="null" /> to skip labels.</param>
        /// <param name="reviewers">Reviewers to add; currently unused by this method.</param>
        /// <returns>The new pull request id, or <c>0</c> if creation failed. Label failures do not change this value.</returns>
        Task<int> GeneratePullRequest(
            string repositoryId,
            string title,
            string description,
            string sourceBranch,
            string targetBranch,
            bool enableAutoComplete,
            IReadOnlyList<string>? labels = null,
            params object[] reviewers);
        Task<int> CreateBranchAndPullRequestAsync(string sourceBranch, string targetBranch, string repositoryId, int prIdOrig);
        Task<bool> CompletePullRequestAsync(string repositoryId, int pullRequestId);
        Task AbandonarPullRequestAsync(string repositoryId, int pullRequestId);
        Task<AzureDevopsPullRequest?> GetPullRequestAsync(int pullRequestId);
        Task<AzureDevopsPullRequest?> GetPullRequestAsync(string projectName, int pullRequestId);
        Task<List<AzureDevopsPullRequest>> GetPullRequestsAsync(string repositoryId, string? status = null, int? top = null);
        Task<PullRequestHistory> GetPullRequestHistoryAsync(string repositoryId, int prId);
        Task AddReviewerToPR(string repositoryId, int prId, params string[] reviewerDescriptors);
        Task VoteOnPullRequestWithPatAsync(string pat, string AdName, string repositoryId, int pullRequestId, int voteValue, string? comments = null);
        Task EnableAutoCompleteForPR(string repositoryId, int prId, bool deleteSourceBranch = true, string mergeStrategy = "merge");
        Task<PRCompleteData> GetPRCompleteDataAsync(int pullRequestId);
        Task<PRCompleteDataWithDiffs> GetPRCompleteDataWithDiffsAsync(int pullRequestId);
        string GenerateFileDiffView(FileDetailedDiff fileDiff);
        string GeneratePortalLikeView(PRCompleteDataWithDiffs prData);
        Task<PullRequestInfo?> ObtenerInfoPullRequestAsync(int prId);
    }
}
