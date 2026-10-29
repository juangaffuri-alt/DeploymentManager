namespace PasadorScript.Services.Azure
{
    public interface IAzureRepositoryService
    {
        Task<ICollection<DTO.AzureDev.Repository>> GetRepositories();
        Task<int> GetDefinitionIdByBuildName(string repositoryName);
    }
}
