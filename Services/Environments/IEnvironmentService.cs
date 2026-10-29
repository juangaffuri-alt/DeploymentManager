using PasadorScript.DTO.AzureDev;

namespace PasadorScript.Services.Environments
{
    public interface IEnvironmentService
    {
        Task<ICollection<EnvironmentDto>> GetEnvironmentDtosGxAsync(string? repositoryId = null, bool nativeBT = true);
        Task<ICollection<EnvironmentDto>> GetEnvironmentDtosScriptsAsync(string? repositoryId = null);
    }
}