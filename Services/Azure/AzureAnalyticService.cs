using PasadorScript.DTO.AzureDev;
using System.Text.Json;

namespace PasadorScript.Services.Azure
{
    public class AzureAnalyticService: AzureBaseService, IAzureAnalyticService
    {
        private readonly ILogger<AzureAnalyticService> _logger;
        private readonly AzureHttpClientConfigured _httpConfigured;
        private readonly AzureHttpClientUser _httpUser;
        private readonly AzureHttpClientLegacy _httpUserLegacy;

        public AzureAnalyticService(IConfiguration configuration, ILogger<AzureAnalyticService> logger,
            AzureHttpClientConfigured azureHttpClientConfigured, AzureHttpClientUser httpUser, 
            AzureHttpClientLegacy azureHttpClientLegacy):base(configuration)
        {
            _logger = logger;
            _httpConfigured = azureHttpClientConfigured;
            _httpUser = httpUser;
            _httpUserLegacy = azureHttpClientLegacy;
        }

        public async Task<List<AgentPool>> GetAgentPoolsAsync()
        {
            var baseUrl = Ctx(ProjectType);
            var url = $"{baseUrl.AzureDevopsHost}/{baseUrl.Organization}/_apis/distributedtask/pools?api-version=7.1-preview.1";
            _httpConfigured.IgnoreCaseSensitive = true;
            var responseAzure = await _httpConfigured.GetAsync<AgentPoolResponseAzure>(url);
            var content = JsonSerializer.Serialize(responseAzure?.Value);
            var json = JsonSerializer.Deserialize<ICollection<AgentPool>>(content);
            var list = json?.Where(x => !x.IsLegacy).ToList();
            foreach (var item in list!)
            {
                var agent = await GetAgentsByPoolIdAsync(item.Id);
                item.Agents = agent;
            }
            return list ?? new List<AgentPool>();
        }
        public async Task<PipelineAnalyticsResult?> GetPipelineAnalyticsAsync(int? definitionId)
        {
            string baseUrl = Ctx(ProjectType).BaseUrl;
            var buildsUrl = $"{baseUrl}/_apis/build/builds?definitions={definitionId}&$top=100&api-version=7.1-preview.7";
            var response = await _httpConfigured.GetAsync(buildsUrl);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var builds = doc.RootElement.GetProperty("value");

            int total = 0, success = 0;
            List<double> durations = new();

            foreach (var build in builds.EnumerateArray())
            {
                total++;

                var result = build.GetProperty("result").GetString();
                if (result == "succeeded")
                    success++;

                if (build.TryGetProperty("startTime", out var startProp) &&
                    build.TryGetProperty("finishTime", out var finishProp) &&
                    DateTime.TryParse(startProp.GetString(), out var startTime) &&
                    DateTime.TryParse(finishProp.GetString(), out var finishTime))
                {
                    durations.Add((finishTime - startTime).TotalSeconds);
                }
            }

            return new PipelineAnalyticsResult
            {
                TotalRuns = total,
                SuccessfulRuns = success,
                AverageDurationSeconds = durations.Count > 0 ? durations.Average() : 0
            };
        }
        //Helpers
        private async Task<List<Agent>> GetAgentsByPoolIdAsync(int poolId)
        {
            var baseUrl = Ctx(ProjectType);
            var url = $"{baseUrl.AzureDevopsHost}/{baseUrl.Organization}/_apis/distributedtask/pools/{poolId}/agents?api-version=7.1-preview.1";
            var response = await _httpConfigured.GetAsync(url);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            var json = await JsonSerializer.DeserializeAsync<AgentResponseAzure>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return JsonSerializer.Deserialize<List<Agent>>(JsonSerializer.Serialize(json?.Value)) ?? new List<Agent>();
        }
    }
    
}
