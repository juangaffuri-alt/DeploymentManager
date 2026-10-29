using PasadorScript.DTO.Git;

namespace PasadorScript.Services.Git
{
    public interface IGitService
    {
        public string ProjectDevops { get; set; }
        public string RepositoryId { get; set; }
        Task<MergeRevertResult> RevertMergeAndCreatePr(int completedPullRequestId, RevertStrategy strategy);
        Task<bool> CanRevertPullRequest(int pullRequestId);
        Task<GitCommit> GetMergeCommit(int pullRequestId);
        Task<string> CreateRevertCommit(string mergeCommitId, string revertMessage);
        Task<string> CreateBranchFromCommit(string branchName, string commitId);
        Task<int> CreatePullRequestFromRevert(string sourceBranch, string targetBranch, int originalPrId);
        Task<bool> IsMergeCommit(string commitId);
        Task<RecreationResult> RecreatePullRequest(int completedPullRequestId);
        Task<RecreationResult> RecreatePullRequestManual(int completedPullRequestId);
        Task AddReviewerToPR(int pullRequestId, params string[] reviewerDescriptors);
        Task EnableAutoCompleteForPR(int pullRequestId, bool deleteSourceBranch = true, string mergeStrategy = "merge");
    }
}
