using PasadorScript.DTO.AzureDev;
using System.Text.Json;
using System.Web;

namespace PasadorScript.Services.Azure
{
    public class AzureCommitService: AzureBaseService, IAzureCommitService
    {
        private readonly ILogger<AzureCommitService> _logger;
        private readonly AzureHttpClientConfigured _httpConfigured;
        private readonly AzureHttpClientUser _httpUser;
        private readonly AzureHttpClientLegacy _httpUserLegacy;

        public AzureCommitService(IConfiguration configuration, ILogger<AzureCommitService> logger,
            AzureHttpClientConfigured azureHttpClientConfigured, AzureHttpClientUser httpUser, AzureHttpClientLegacy azureHttpClientLegacy) : base(configuration)
        {
            _logger = logger;
            _httpConfigured = azureHttpClientConfigured;
            _httpUser = httpUser;
            _httpUserLegacy = azureHttpClientLegacy;
        }

        public async Task<ICollection<CommitStatusesResponse>> GetCommitStatusesAsync(string repoId, string commitId)
        {
            string baseUrl = Ctx(ProjectType).BaseUrl;
            string url = $"{baseUrl}/_apis/git/repositories/{repoId}/commits/{commitId}/statuses?api-version=7.1";
            var resp = await _httpConfigured.GetAsync(url);
            resp.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var list = doc.RootElement.GetProperty("value")
            .EnumerateArray()
            .Select(status => (
                status.GetProperty("state").GetString()!,
                status.GetProperty("description").GetString()!,
                status.GetProperty("targetUrl").GetString() ?? string.Empty,
                ExtractBuildIdFromTargetUrl(status.GetProperty("targetUrl").GetString() ?? string.Empty)
            )).ToList();
            var res = new List<CommitStatusesResponse>();
            foreach (var item in list)
            {
                res.Add(new CommitStatusesResponse()
                {
                    State = item.Item1,
                    Description = item.Item2,
                    TargetUrl = item.Item3,
                    BuildId = item.Item4,
                });
            }
            return res;
        }

        public async Task<List<CommitHistory>> GetFileHistoryAsync(string repositoryId, string filePath, int maxResults = 50)
        {
            try
            {
                var baseUrl = Ctx(ProjectType).BaseUrl;
                // URL de la API de Azure DevOps para obtener el historial de commits de un archivo
                var url = $"{baseUrl}/_apis/git/repositories/{repositoryId}/commits?api-version=7.1&searchCriteria.itemPath={Uri.EscapeDataString(filePath)}&searchCriteria.$top={maxResults}";

                var response = await _httpConfigured.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var azureDevOpsResponse = JsonSerializer.Deserialize<AzureDevOpsResponse>(jsonResponse, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    return MapToCommitHistory(azureDevOpsResponse!);
                }
                else
                {
                    throw new HttpRequestException($"Error al obtener el historial: {response.StatusCode} - {response.ReasonPhrase}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener el historial del archivo {FilePath} en el repositorio {RepositoryId}", filePath, repositoryId);
                throw new Exception($"Error al obtener el historial del archivo: {ex.Message}", ex);
            }
        }

        public async Task<string> GetFileContentAtCommit(string repositoryId, string commitId, string filePath)
        {
            var _baseUrl = Ctx(ProjectType).BaseUrl;
            var url = $"{_baseUrl}/_apis/git/repositories/{repositoryId}/items";

            var queryParams = new Dictionary<string, string>
            {
                ["path"] = filePath,
                ["versionDescriptor.versionType"] = "commit",
                ["versionDescriptor.version"] = commitId,
                ["api-version"] = "7.0"
            };

            var queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
            var fullUrl = $"{url}?{queryString}";

            var response = await _httpConfigured.GetAsync(fullUrl);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return string.Empty;

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        public async Task<List<CommitHistory>> GetFileHistoryByBranchAsync(string repositoryId, string filePath, string branch = "main", int maxResults = 50)
        {
            try
            {
                var baseUrl = Ctx(ProjectType).BaseUrl;
                // URL de la API de Azure DevOps para obtener el historial de commits de un archivo por branch
                var url = $"{baseUrl}/_apis/git/repositories/{repositoryId}/commits?api-version=7.1&searchCriteria.itemPath={Uri.EscapeDataString(filePath)}&searchCriteria.$top={maxResults}&searchCriteria.itemVersion.version={branch}";

                var response = await _httpConfigured.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var azureDevOpsResponse = JsonSerializer.Deserialize<AzureDevOpsResponse>(jsonResponse, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    return MapToCommitHistory(azureDevOpsResponse!);
                }
                else
                {
                    throw new HttpRequestException($"Error al obtener el historial: {response.StatusCode} - {response.ReasonPhrase}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener el historial del archivo {FilePath} en el repositorio {RepositoryId} para la rama {Branch}", filePath, repositoryId, branch);
                throw new Exception($"Error al obtener el historial del archivo: {ex.Message}", ex);
            }
        }

        public async Task<string?> GetMergeCommitIdAsync(int prId)
        {
            string baseUrl = Ctx(ProjectType).BaseUrl;
            string url = $"{baseUrl}/_apis/git/pullrequests/{prId}?api-version=7.1";
            var resp = await _httpConfigured.GetAsync(url);
            resp.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            return doc.RootElement
                .GetProperty("lastMergeCommit")
                .GetProperty("commitId")
                .GetString();
        }
        //Helpers
        private static int? ExtractBuildIdFromTargetUrl(string targetUrl)
        {
            if (string.IsNullOrWhiteSpace(targetUrl))
                return null;

            // Caso 1: URL normal con querystring (?buildId=1234)
            if (Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri))
            {
                var queryParams = HttpUtility.ParseQueryString(uri.Query);
                if (int.TryParse(queryParams.Get("buildId"), out int idFromQuery))
                    return idFromQuery;
            }

            // Caso 2: vstfs URI -> vstfs:///Build/Build/3314
            var vstfsPrefix = "vstfs:///Build/Build/";
            if (targetUrl.StartsWith(vstfsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var idPart = targetUrl.Substring(vstfsPrefix.Length);
                if (int.TryParse(idPart, out int idFromVstfs))
                    return idFromVstfs;
            }

            return null;
        }
        private List<CommitHistory> MapToCommitHistory(AzureDevOpsResponse azureDevOpsResponse)
        {
            if (azureDevOpsResponse?.Value == null)
                return new List<CommitHistory>();

            return azureDevOpsResponse.Value.Select(commit => new CommitHistory
            {
                CommitId = commit.CommitId,
                Author = commit.Author?.Name ?? "Unknown",
                Date = commit.Author?.Date ?? commit.Date,
                Message = commit.Comment ?? "No message"
            }).ToList();
        }
    }
}
