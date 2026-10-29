using PasadorScript.DTO.AzureDev;

namespace PasadorScript.Services.Azure
{
    public interface IAzureCommitService
    {
        Task<string?> GetMergeCommitIdAsync(int prId);
        Task<ICollection<CommitStatusesResponse>> GetCommitStatusesAsync(string repoId, string commitId);
        Task<List<CommitHistory>> GetFileHistoryAsync(string repositoryId, string filePath, int maxResults = 50);
        Task<List<CommitHistory>> GetFileHistoryByBranchAsync(string repositoryId, string filePath, string branch = "main", int maxResults = 50);
        Task<string> GetFileContentAtCommit(string repositoryId, string commitId, string filePath);
    }
}
