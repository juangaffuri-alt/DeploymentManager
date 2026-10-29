
using System.Text.Json;

namespace PasadorScript.Services.Azure
{
    public class AzureRepositoryService: AzureBaseService, IAzureRepositoryService
    {
        private readonly ILogger<AzureRepositoryService> _logger;
        private readonly AzureHttpClientConfigured _httpConfigured;
        private readonly AzureHttpClientUser _httpUser;
        private readonly AzureHttpClientLegacy _httpUserLegacy;

        public AzureRepositoryService(IConfiguration configuration, ILogger<AzureRepositoryService> logger,
            AzureHttpClientConfigured azureHttpClientConfigured, AzureHttpClientUser httpUser, AzureHttpClientLegacy azureHttpClientLegacy) : base(configuration)
        {
            _logger = logger;
            _httpConfigured = azureHttpClientConfigured;
            _httpUser = httpUser;
            _httpUserLegacy = azureHttpClientLegacy;
        }

        public async Task<ICollection<DTO.AzureDev.Repository>> GetRepositories()
        {
            string url = $"{Ctx(ProjectType).BaseUrl}/_apis/git/repositories?api-version=7.1-preview.1";
            ICollection<DTO.AzureDev.Repository> res = new List<DTO.AzureDev.Repository>();

            try
            {
                HttpResponseMessage response = await _httpConfigured.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var repos = doc.RootElement.GetProperty("value");

                foreach (var repo in repos.EnumerateArray())
                {
                    string name = repo.GetProperty("name").GetString()!;
                    string id = repo.GetProperty("id").GetString()!;
                    res.Add(new DTO.AzureDev.Repository
                    {
                        IdRepo = id,
                        Name = name
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error al obtener repositorios");
            }
            return res;
        }

        public async Task<int> GetDefinitionIdByBuildName(string repositoryName)
        {
            //solo las de tipo yaml
            string url = $"{Ctx(ProjectType).BaseUrl}/_apis/build/definitions?api-version=7.1&processType=2";
            try
            {
                HttpResponseMessage response = await _httpConfigured.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var repos = doc.RootElement.GetProperty("value");

                var list = repos.EnumerateArray();
                var field = list.Where(x => x.GetProperty("name").ToString().Equals(repositoryName, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
                return int.Parse(field.GetProperty("id").ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error al obtener repositorios");
                return -1;
            }
        }
    }
}
