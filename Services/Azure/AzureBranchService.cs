
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PasadorScript.Services.Azure
{
    public class AzureBranchService : AzureBaseService, IAzureBranchService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<AzureBranchService> _logger;
        private readonly AzureHttpClientConfigured _httpConfigured;
        private readonly AzureHttpClientUser _httpUser;
        private readonly AzureHttpClientLegacy _httpUserLegacy;

        public AzureBranchService(IConfiguration configuration, ILogger<AzureBranchService> logger,
            AzureHttpClientConfigured azureHttpClientConfigured, AzureHttpClientUser httpUser,
            AzureHttpClientLegacy azureHttpClientLegacy) : base(configuration)
        {
            _config = configuration;
            _logger = logger;
            _httpConfigured = azureHttpClientConfigured;
            _httpUser = httpUser;
            _httpUserLegacy = azureHttpClientLegacy;
        }
        public async Task<bool> CreateBranchAsync(string repositoryId, string newBranchName)
        {
            // 1. Obtener el último commit (objectId) del branch base
            var refName = await GetDefaultBranch(repositoryId);
            //_apis/git/repositories/{3}/refs?filter=heads/{4}&api-version=7.0

            var refUrl = $"{Ctx(ProjectType).BaseUrl}/_apis/git/repositories/{repositoryId}/refs?filter=heads/{refName}&api-version=7.0";

            var refResponse = await _httpConfigured.GetAsync(refUrl);
            if (!refResponse.IsSuccessStatusCode)
            {
                _logger.LogError($"Error obteniendo branch base: {refResponse.StatusCode}");
                return false;
            }

            var refJson = await refResponse.Content.ReadAsStringAsync();
            using var refDoc = JsonDocument.Parse(refJson);
            var objectId = refDoc.RootElement.GetProperty("value")[0].GetProperty("objectId").GetString();

            // 2. Crear la nueva rama apuntando al mismo commit
            //_apis/git/repositories/{3}/refs?api-version=7.1-preview.1
            var createRefUrl = $"{Ctx(ProjectType).BaseUrl}/_apis/git/repositories/{repositoryId}/refs?api-version=7.1-preview.1";
            var payload = new[]
            {
                new {
                    name = $"refs/heads/{newBranchName}",
                    oldObjectId = "0000000000000000000000000000000000000000",
                    newObjectId = objectId
                }
            };

            var createResponse = await _httpUser.SendAsync(HttpMethod.Post, createRefUrl, payload);
            if (createResponse.IsSuccessStatusCode)
            {
                _logger.LogDebug($"Rama creada correctamente: {repositoryId}/{newBranchName}");
                return true;
            }

            var error = await createResponse.Content.ReadAsStringAsync();
            _logger.LogError($"Error creando rama: {createResponse.StatusCode}\n{error}");
            return false;
        }


        public async Task<bool> DeleteBranchAsync(string repositoryId, string branchName)
        {
            var url = $"{Ctx(ProjectType).BaseUrl}/_apis/git/repositories/{repositoryId}/refs?api-version=7.1-preview.1";

            // Branch en Azure DevOps siempre se referencia con "refs/heads/"
            var refName = $"refs/heads/{branchName}";

            var payload = new[]
            {
                new
                {
                    name = refName,
                    oldObjectId = await GetBranchObjectIdAsync(repositoryId, branchName),
                    newObjectId = "0000000000000000000000000000000000000000"
                }
            };
            var response = await _httpUser.SendAsync(HttpMethod.Post, url, payload);
            return response.IsSuccessStatusCode;
        }

        public async Task<string> GetDefaultBranch(string repoId)
        {
            var repoUrl = $"{Ctx(ProjectType).BaseUrl}/_apis/git/repositories/{repoId}?api-version=7.0";
            var repoResponse = await _httpConfigured.GetAsync(repoUrl);
            repoResponse.EnsureSuccessStatusCode();

            using (var repoStream = await repoResponse.Content.ReadAsStreamAsync())
            {
                using var repoJson = await JsonDocument.ParseAsync(repoStream);
                var defaultBranchFull = repoJson.RootElement.GetProperty("defaultBranch").GetString(); // e.g. "refs/heads/main"
                if (!string.IsNullOrEmpty(defaultBranchFull) && defaultBranchFull.StartsWith("refs/heads/"))
                {
                    return defaultBranchFull.Replace("refs/heads/", "");
                }
            }
            return null;
        }
        private async Task<string> GetBranchObjectIdAsync(string repositoryId, string branchName)
        {

            var url = $"{Ctx(ProjectType).BaseUrl}/_apis/git/repositories/{repositoryId}/refs/heads/{branchName}?api-version=7.1-preview.1";

            var response = await _httpConfigured.GetAsync(url);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var value = doc.RootElement.GetProperty("value");
            if (value.GetArrayLength() == 0)
                throw new Exception($"Branch '{branchName}' no encontrado en {repositoryId}");

            return value[0].GetProperty("objectId").GetString();
        }
    }
}
