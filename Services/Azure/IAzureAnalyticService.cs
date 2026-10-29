using PasadorScript.DTO.AzureDev;

namespace PasadorScript.Services.Azure
{
    public interface IAzureAnalyticService
    {
        Task<List<AgentPool>> GetAgentPoolsAsync();
        Task<PipelineAnalyticsResult?> GetPipelineAnalyticsAsync(int? definitionId);
    }
}
