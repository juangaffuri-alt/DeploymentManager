namespace PasadorScript.Services.Azure
{
    public interface IAzureBranchService
    {
        Task<bool> CreateBranchAsync(string repositoryId, string newBranchName);
        Task<bool> DeleteBranchAsync(string repositoryId, string branchName);
        Task<string> GetDefaultBranch(string repoId);
    }
}
