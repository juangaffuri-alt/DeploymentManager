using Newtonsoft.Json.Linq;
using PasadorScript.DTO.AzureDev;
using PasadorScript.Services.Pat;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PasadorScript.Services.Azure
{
    public class AzureBuildService:AzureBaseService, IAzureBuildService
    {
        private readonly ILogger<AzureBuildService> _logger;
        private readonly IPatRepositoryService _patService;
        private readonly AzureHttpClientConfigured _httpConfigured;
        private readonly IAzurePullRequestService _azurePullRequestService;

        public AzureBuildService(IConfiguration configuration, ILogger<AzureBuildService> logger,
            IPatRepositoryService patRepositoryService, AzureHttpClientConfigured azureHttpClientConfigured, IAzurePullRequestService azurePullRequestService) : base(configuration)
        {
            _logger = logger;
            _patService = patRepositoryService;
            _httpConfigured = azureHttpClientConfigured;
            _azurePullRequestService = azurePullRequestService;
        }

        public async Task<BuildInfo> BuscarBuildPorCommit(string commitId, string? statusFilter, string repository = "")
        {
            var _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            string baseUrl = Ctx(ProjectType).BaseUrl;
            //var filter = !String.IsNullOrEmpty(statusFilter) ? $"&statusFilter={statusFilter}" : "";
            var filter = !string.IsNullOrEmpty(repository) ? $"&repositoryId={repository}" : "";
            //var buildsUrl = $"{baseUrl}/_apis/build/builds?commitId={commitId}{filter}&queryOrder=finishTimeDescending&$top=1&api-version=7.1-preview.7";
            var buildsUrl = $"{baseUrl}/_apis/build/builds?sourceVersion={commitId}{filter}&repositoryType=TfsGit&api-version=7.1-preview.7";

            var response = await _httpConfigured.GetAsync(buildsUrl);
            if (!response.IsSuccessStatusCode) return null;

            var content = await response.Content.ReadAsStringAsync();
            var buildsResponse = JsonSerializer.Deserialize<BuildsResponse>(content, _jsonOptions);

            return buildsResponse?.Value?.FirstOrDefault()!;
        }
        public async Task<int?> FindBuildDefinitionAsync(string repositoryId, string repositoryName, string branchName)
        {
            try
            {
                // URL para obtener build definitions filtradas por repositorio
                var url = $"{Ctx(ProjectType).BaseUrl}/_apis/build/definitions?repositoryId={repositoryId}&repositoryType=TfsGit&api-version=6.0";

                var response = await _httpConfigured.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var definitionsResponse = JsonSerializer.Deserialize<BuildDefList>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                // Buscar la definición que coincida con la rama
                var definition = FindMatchingDefinition(definitionsResponse.Value, repositoryName, branchName);

                return definition?.Id;
            }
            catch (Exception ex)
            {
                // Log error
                _logger.LogError(ex, $"Error finding build definition: {ex.Message}");
                return null;
            }
        }

        public async Task<BuildInfo?> GetBuildByPullRequestAsync(int pullRequestId, int? definitionId, bool fast = false)
        {
            var pullRequest = await _azurePullRequestService.GetPullRequestAsync(pullRequestId);
            var _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var baseUrl = Ctx(ProjectType).BaseUrl;

            // Buscar builds que tengan el PR ID en sus propiedades
            var url = "";
            //if (definitionId != null)
            //{
            //    url = $"{baseUrl}/_apis/build/builds" +
            //     $"?definitions={definitionId}" +
            //     $"&$top=100" + // Aumentamos para tener más builds donde buscar
            //     $"&$orderBy=queueTime desc" +
            //     $"&api-version=6.0";
            //}
            //else
            //{
            if (pullRequest != null && pullRequest.LastMergeCommit != null)
            {
                var prDate = pullRequest.CreationDate.ToUniversalTime().AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ssZ");
                var maxPrDate = pullRequest.CreationDate.ToUniversalTime().AddDays(20).ToString("yyyy-MM-ddTHH:mm:ssZ");
                url = $"{baseUrl}/_apis/build/builds?repositoryId={pullRequest.Repository.Id}&repositoryType=TfsGit&branchName={pullRequest.TargetRefName}" +
                    $"&sourceVersion={pullRequest.LastMergeSourceCommit.CommitId}&$top=1000&minTime={prDate}&maxTime={maxPrDate}&queryOrder=finishTimeDescending&api-version=7.1-preview.7";
            }
            else
                return null;
            //}

            var response = await _httpConfigured.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            var buildsResponse = JsonSerializer.Deserialize<BuildsResponse>(content, _jsonOptions);

            if (buildsResponse?.Value == null)
                return null;


            var relatedBuilds = new List<BuildInfo>();

            // Filtrar builds que corresponden al PR
            foreach (var build in buildsResponse.Value)
            {
                bool isPrRelated = false;

                // Método 1: Verificar en TriggerInfo - ci.message
                if (build.TriggerInfo != null && build.TriggerInfo.ContainsKey("ci.message"))
                {
                    var message = build.TriggerInfo["ci.message"];
                    if (message != null && (
                        message.Contains($"PR #{pullRequestId}") ||
                        message.Contains($"PR{pullRequestId}") ||
                        message.Contains($"#{pullRequestId}") ||
                        message.Contains($"pull request #{pullRequestId}") ||
                        message.Contains($"Auto-completed PR #{pullRequestId}") ||
                        message.Contains($"Merged PR #{pullRequestId}") ||
                        message.Contains($"Merged PR {pullRequestId}") ||
                        message.Contains($"Merge pull request {pullRequestId}")))
                    {
                        isPrRelated = true;
                    }
                }

                // Método 2: Verificar en Parameters
                if (!isPrRelated && build.Parameters != null)
                {
                    if (build.Parameters.Contains($"\"system.pullRequest.pullRequestId\":\"{pullRequestId}\"") ||
                        build.Parameters.Contains($"\"system.pullRequest.pullRequestId\":{pullRequestId}") ||
                        build.Parameters.Contains($"\"PullRequestId\":{pullRequestId}") ||
                        build.Parameters.Contains($"\"PullRequestId\":\"{pullRequestId}\"") ||
                        build.Parameters.Contains($"\"pr.number\":\"{pullRequestId}\"") ||
                        build.Parameters.Contains($"\"pr.number\":{pullRequestId}"))
                    {
                        isPrRelated = true;
                    }
                }

                // Método 3: Verificar en SourceBranch
                if (!isPrRelated && build.SourceBranch != null)
                {
                    if (build.SourceBranch.Contains($"refs/pull/{pullRequestId}/") ||
                        build.SourceBranch.Contains($"pull/{pullRequestId}/") ||
                        build.SourceBranch.EndsWith($"refs/pull/{pullRequestId}/merge") ||
                        build.SourceBranch.Contains($"/{pullRequestId}/"))
                    {
                        isPrRelated = true;
                    }
                }

                // Método 4: Verificar otras propiedades de TriggerInfo
                if (!isPrRelated && build.TriggerInfo != null)
                {
                    if ((build.TriggerInfo.ContainsKey("pr.number") &&
                        build.TriggerInfo["pr.number"] == pullRequestId.ToString()) ||
                        (build.TriggerInfo.ContainsKey("pullRequestId") &&
                        build.TriggerInfo["pullRequestId"] == pullRequestId.ToString()))
                    {
                        isPrRelated = true;
                    }
                }


                if (isPrRelated)
                {
                    relatedBuilds.Add(build);
                    break;
                }
            }

            // Si encontramos builds relacionados al PR
            if (relatedBuilds.Any())
            {
                return relatedBuilds.FirstOrDefault();
            }
            else
            {

                // le pegamos un tiro a los logs, porque a veces que se envia el build manual pierde referencia
                // al pr en cuestion
                BuildInfo? r = null;
                if (!fast)
                {
                    if (pullRequest!.Status == "completed" && pullRequest!.MergeStatus == "succeeded")
                    {
                        foreach (var build in buildsResponse.Value.Take(20))
                        {
                            var prFind = await FindPRNumberInBuildLogsAsync(build.Id, pullRequestId);
                            if (prFind != null && prFind == pullRequestId.ToString())
                            {
                                r = build;
                                break;
                            }
                        }
                    }
                }
                return r;
            }
        }

        public async Task<List<TaskInfo>> GetBuildTimelineAsync(int buildId)
        {
            var baseUrl = Ctx(ProjectType).BaseUrl;
            var url = $"{baseUrl}/_apis/build/builds/{buildId}/timeline?api-version=7.1";
            var res = await _httpConfigured.GetAsync(url);
            res.EnsureSuccessStatusCode();
            var json = await res.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var list = new List<TaskInfo>();
            if (root.TryGetProperty("records", out var records))
            {
                foreach (var r in records.EnumerateArray())
                {
                    var task = new TaskInfo();
                    if (r.TryGetProperty("id", out var p)) task.Id = p.GetString()!;
                    if (r.TryGetProperty("parentId", out var pp)) task.ParentId = pp.GetString()!;
                    if (r.TryGetProperty("name", out var nm)) task.Name = nm.GetString()!;
                    if (r.TryGetProperty("type", out var tp)) task.Type = tp.GetString()!;
                    if (r.TryGetProperty("state", out var sta)) task.State = sta.GetString()!;
                    if (r.TryGetProperty("result", out var rs)) task.Result = rs.GetString()!;

                    if (r.TryGetProperty("startTime", out var st) && st.ValueKind != JsonValueKind.Null)
                        task.StartTime = st.GetDateTime();

                    if (r.TryGetProperty("finishTime", out var ft) && ft.ValueKind != JsonValueKind.Null)
                        task.FinishTime = ft.GetDateTime();

                    // log.id puede no existir
                    if (r.TryGetProperty("log", out var logProp) && logProp.ValueKind == JsonValueKind.Object)
                    {
                        if (logProp.TryGetProperty("id", out var logIdEl) && logIdEl.ValueKind == JsonValueKind.Number)
                            task.LogId = logIdEl.GetInt32();
                    }

                    list.Add(task);
                }
            }

            return list;
        }

        public async Task<BuildStatusResult> GetCompleteInfoPullRequestAndBuild(int pullRequestId)
        {
            try
            {
                // 1. Obtener información del Pull Request
                var pullRequest = await _azurePullRequestService.GetPullRequestAsync(pullRequestId);
                if (pullRequest == null)
                {
                    return new BuildStatusResult
                    {
                        Success = false,
                        ErrorMessage = $"Pull Request {pullRequestId} not found"
                    };
                }

                if (!string.IsNullOrEmpty(pullRequest.LastMergeCommit?.CommitId))
                {
                    var latestBuild = await BuscarBuildPorCommit(pullRequest.LastMergeCommit.CommitId, null, pullRequest.Repository.Id);
                    if (latestBuild == null)
                    {
                        return new BuildStatusResult
                        {
                            Success = false,
                            // Información del Pull Request
                            PullRequestId = pullRequest.PullRequestId,
                            PullRequestStatus = pullRequest.Status,
                            PullRequestTitle = pullRequest.Title,
                            SourceRefName = pullRequest.SourceRefName,
                            TargetRefName = pullRequest.TargetRefName
                        };
                    }
                    return new BuildStatusResult
                    {
                        DefinitionId = latestBuild.Definition.Id,
                        Success = true,
                        BuildId = latestBuild.Id,
                        Status = latestBuild.Status,
                        Result = latestBuild.Result,
                        StartTime = latestBuild.StartTime,
                        FinishTime = latestBuild.FinishTime,
                        // Información del Pull Request
                        PullRequestId = pullRequest.PullRequestId,
                        PullRequestStatus = pullRequest.Status,
                        PullRequestTitle = pullRequest.Title,
                        SourceRefName = pullRequest.SourceRefName,
                        TargetRefName = pullRequest.TargetRefName
                    };
                }
                else
                {
                    return new BuildStatusResult
                    {
                        Success = false,
                        // Información del Pull Request
                        PullRequestId = pullRequest.PullRequestId,
                        PullRequestStatus = pullRequest.Status,
                        PullRequestTitle = pullRequest.Title,
                        SourceRefName = pullRequest.SourceRefName,
                        TargetRefName = pullRequest.TargetRefName
                    };
                }



            }
            catch (Exception ex)
            {
                return new BuildStatusResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<BuildStatusResult> GetCompleteInfoPullRequestAndBuild(int pullRequestId, int definitionId)
        {
            try
            {
                // 1. Obtener información del Pull Request
                var pullRequest = await _azurePullRequestService.GetPullRequestAsync(pullRequestId);
                if (pullRequest == null)
                {
                    return new BuildStatusResult
                    {
                        Success = false,
                        ErrorMessage = $"Pull Request {pullRequestId} not found"
                    };
                }

                var latestBuild = await GetBuildByPullRequestAsync(pullRequestId, definitionId);
                if (latestBuild == null)
                {
                    // 2. Obtener el último build para el PR y definition
                    latestBuild = await GetLatestBuildAsync(pullRequest.SourceRefName, definitionId);
                    if (latestBuild == null)
                    {
                        return new BuildStatusResult
                        {
                            Success = false,
                            // Información del Pull Request
                            PullRequestId = pullRequest.PullRequestId,
                            PullRequestStatus = pullRequest.Status,
                            PullRequestTitle = pullRequest.Title,
                            SourceRefName = pullRequest.SourceRefName,
                            TargetRefName = pullRequest.TargetRefName
                        };
                    }
                }

                return new BuildStatusResult
                {
                    Success = true,
                    BuildId = latestBuild.Id,
                    Status = latestBuild.Status,
                    Result = latestBuild.Result,
                    StartTime = latestBuild.StartTime,
                    FinishTime = latestBuild.FinishTime,
                    // Información del Pull Request
                    PullRequestId = pullRequest.PullRequestId,
                    PullRequestStatus = pullRequest.Status,
                    PullRequestTitle = pullRequest.Title,
                    SourceRefName = pullRequest.SourceRefName,
                    TargetRefName = pullRequest.TargetRefName
                };
            }
            catch (Exception ex)
            {
                return new BuildStatusResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<BuildStatusResult> GetCompleteInfoPullRequestAndBuildFast(int pullRequestId, int definitionId)
        {
            try
            {
                // 1. Obtener información del Pull Request
                var pullRequest = await _azurePullRequestService.GetPullRequestAsync(pullRequestId);
                if (pullRequest == null)
                {
                    return new BuildStatusResult
                    {
                        Success = false,
                        ErrorMessage = $"Pull Request {pullRequestId} not found"
                    };
                }

                var latestBuild = await GetBuildByPullRequestAsync(pullRequestId, definitionId, true);
                if (latestBuild == null)
                {
                    return null;
                }

                return new BuildStatusResult
                {
                    Success = true,
                    BuildId = latestBuild.Id,
                    Status = latestBuild.Status,
                    Result = latestBuild.Result,
                    StartTime = latestBuild.StartTime,
                    FinishTime = latestBuild.FinishTime,
                    // Información del Pull Request
                    PullRequestId = pullRequest.PullRequestId,
                    PullRequestStatus = pullRequest.Status,
                    PullRequestTitle = pullRequest.Title,
                    SourceRefName = pullRequest.SourceRefName,
                    TargetRefName = pullRequest.TargetRefName
                };
            }
            catch (Exception ex)
            {
                return new BuildStatusResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<int?> GetDefinitionIdByBuildId(int buildId)
        {
            var baseUrl = Ctx(ProjectType).BaseUrl;
            var defUrl = $"{baseUrl}/_apis/build/builds/{buildId}?api-version=7.1";
            var defResp = await _httpConfigured.GetAsync(defUrl);
            var defDoc = JsonDocument.Parse(await defResp.Content.ReadAsStringAsync());
            return defDoc.RootElement.GetProperty("definition").GetProperty("id").GetInt32();
        }

        public async Task<string> GetLogContentAsync(int buildId, int logId)
        {
            var baseUrl = Ctx(ProjectType).BaseUrl;
            var url = $"{baseUrl}/_apis/build/builds/{buildId}/logs/{logId}?api-version=7.1";
            var response = await _httpConfigured.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> GetLogForTaskAsync(int buildId, string taskName)
        {
            // Paso 1: Obtener timeline
            var baseUrl = Ctx(ProjectType).BaseUrl;
            var timelineUrl = $"{baseUrl}/_apis/build/builds/{buildId}/timeline?api-version=7.1-preview.2";
            var timelineResponse = await _httpConfigured.GetAsync(timelineUrl);
            timelineResponse.EnsureSuccessStatusCode();

            var json = await timelineResponse.Content.ReadAsStringAsync();
            var timeline = JsonNode.Parse(json)?["records"]?.AsArray();

            if (timeline == null)
                return "No timeline encontrada.";

            foreach (var record in timeline)
            {
                var name = record?["name"]?.ToString();
                var logId = record?["log"]?["id"]?.GetValue<int>();

                if (string.Equals(name, taskName, StringComparison.OrdinalIgnoreCase) && logId.HasValue)
                {
                    // Paso 2: Obtener log específico
                    var logUrl = $"{baseUrl}/_apis/build/builds/{buildId}/logs/{logId}?api-version=7.1-preview.2";
                    var logResponse = await _httpConfigured.GetAsync(logUrl);
                    logResponse.EnsureSuccessStatusCode();
                    return await logResponse.Content.ReadAsStringAsync();
                }
            }

            return $"No se encontró la tarea '{taskName}' con logs asociados.";
        }

        

        public async Task<int> TriggerBuildManual(int buildDefinitionId, string owner, int pullRequestId, string sourceBranch)
        {
            try
            {
                var patOwner = await _patService.GetPatByUser(owner);
                using var httpClient = new HttpClient();
                var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{patOwner}"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                var buildRequest = new BuildTriggerRequest
                {
                    Definition = new Definition { Id = buildDefinitionId },
                    SourceBranch = sourceBranch,
                    Parameters = $"{{\"PullRequestId\":\"{pullRequestId}\",\"IsManualTrigger\":\"true\"}}"
                };
                var baseUrl = Ctx(ProjectType).BaseUrl;
                var url = $"{baseUrl}/_apis/build/builds?api-version=7.0";

                var response = await httpClient.PostAsJsonAsync(url, buildRequest);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseContent);
                var buildId = doc.RootElement.GetProperty("id").GetInt32();

                _logger.LogInformation("Build {BuildId} disparado manualmente para PR {PullRequestId}",
                    buildId, pullRequestId);

                return buildId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disparando build para PR {PullRequestId}", pullRequestId);
                return 0;
            }
        }

        public async Task<int?> GetPrIdByBuildId(string projectName, int buildId)
        {
            try
            {
                string baseUrl = Ctx(projectName).BaseUrl;
                string url = $"{baseUrl}/_apis/build/builds/{buildId}?api-version=6.0";
                var response = await _httpConfigured.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var build = JObject.Parse(json);
                    // Intentar obtener la descripción del mensaje del commit/PR
                    string description = build["message"]?.ToString()
                                         ?? build["triggerInfo"]?["ci.message"]?.ToString()
                                         ?? build["sourceVersionMessage"]?.ToString()
                                         ?? "";

                    // Busca patrones como: PR #1234, pull request #1234, Merge pull request #1234
                    var match = Regex.Match(description, @"(?i)(?:pull\s+request|pr)\s*#(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int prId))
                        return prId;
                    return null;
                }
            }
            catch
            {
                // Si falla, devolvemos vacío
            }
            return null;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<BuildInfo>> GetBuildsByIdsAsync(
            string projectName,
            IReadOnlyCollection<int> buildIds)
        {
            if (string.IsNullOrWhiteSpace(projectName) || buildIds is null || buildIds.Count == 0)
                return Array.Empty<BuildInfo>();

            var distinctIds = buildIds.Where(id => id > 0).Distinct().ToList();
            if (distinctIds.Count == 0)
                return Array.Empty<BuildInfo>();

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var result = new List<BuildInfo>(distinctIds.Count);
            const int batchSize = 100;

            foreach (var chunk in distinctIds.Chunk(batchSize))
            {
                try
                {
                    var ids = string.Join(",", chunk);
                    var url = $"{Ctx(projectName).BaseUrl}/_apis/build/builds?buildIds={ids}&api-version=7.1";
                    var response = await _httpConfigured.GetAsync(url);
                    var content = await response.Content.ReadAsStringAsync();
                    var buildsResponse = JsonSerializer.Deserialize<BuildsResponse>(content, jsonOptions);
                    if (buildsResponse?.Value == null)
                        continue;

                    result.AddRange(buildsResponse.Value);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error obteniendo builds en lote para el proyecto {Project}", projectName);
                }
            }

            return result;
        }

        //Helpers
        private BuildDef FindMatchingDefinition(List<BuildDef> definitions, string repositoryName, string branchName)
        {
            // Estrategia 1: Buscar por nombre de repositorio en la definición
            var definition = definitions.FirstOrDefault(def =>
                def.Name.Contains(repositoryName, StringComparison.OrdinalIgnoreCase) &&
                def.Name.Contains(branchName, StringComparison.OrdinalIgnoreCase));

            if (definition != null) return definition;

            // Estrategia 2: Buscar solo por repositorio
            definition = definitions.FirstOrDefault(def =>
                def.Name.Contains(repositoryName, StringComparison.OrdinalIgnoreCase));

            if (definition != null) return definition;

            // Estrategia 3: Buscar por patrones comunes para la rama
            definition = definitions.FirstOrDefault(def =>
                def.Name.Contains(branchName, StringComparison.OrdinalIgnoreCase) ||
                (branchName == "main" && def.Name.Contains("master", StringComparison.OrdinalIgnoreCase)) ||
                (branchName == "master" && def.Name.Contains("main", StringComparison.OrdinalIgnoreCase)));

            if (definition != null) return definition;

            // Estrategia 4: Buscar por builds de CI genéricos
            definition = definitions.FirstOrDefault(def =>
                def.Name.Contains("CI", StringComparison.OrdinalIgnoreCase) ||
                def.Name.Contains("Continuous", StringComparison.OrdinalIgnoreCase) ||
                def.Name.Contains("Build", StringComparison.OrdinalIgnoreCase));

            return definition!;
        }
        public async Task<string?> FindPRNumberInBuildLogsAsync(int buildId, int pullRequestId)
        {
            try
            {
                // Obtener lista de logs del build
                var logs = await GetBuildLogs(buildId);

                foreach (var log in logs!.Value)
                {
                    // Obtener contenido de cada log
                    var logContent = await GetLogContent(buildId, log.Id);

                    // Buscar referencias a PR
                    var prNumber = FindPRInLogContent(logContent, pullRequestId);
                    if (!string.IsNullOrEmpty(prNumber))
                    {
                        return prNumber;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error en FindPRNumberInBuildLogsAsync: {ex.Message}");
                return null;
            }
        }
        private async Task<LogsResponse?> GetBuildLogs(int buildId)
        {
            var _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var baseUrl = Ctx(ProjectType).BaseUrl;
            var url = $"{baseUrl}/_apis/build/builds/{buildId}/logs?api-version=7.0";
            var response = await _httpConfigured.GetAsync(url);
            return JsonSerializer.Deserialize<LogsResponse>(await response.Content.ReadAsStringAsync()!, _jsonOptions);
        }
        private async Task<string> GetLogContent(int buildId, int logId)
        {
            var baseUrl = Ctx(ProjectType).BaseUrl;
            var url = $"{baseUrl}/_apis/build/builds/{buildId}/logs/{logId}?api-version=7.0";
            var r = await _httpConfigured.GetAsync(url);
            return await r.Content.ReadAsStringAsync();
        }
        private string? FindPRInLogContent(string logContent, int pullRequest)
        {
            // Escapar el número del PR para regex
            var prNumber = Regex.Escape(pullRequest.ToString());

            // Patrones específicos para el PR que buscas
            var patterns = new[]
            {
                $@"PR #({prNumber})",                    // PR #740
                $@"pull request #({prNumber})",          // pull request #740  
                $@"Auto-completed PR #({prNumber})",     // Auto-completed PR #740
                $@"#{prNumber}.*pull request",           // #740 followed by "pull request"
                $@"Merge pull request #({prNumber})",    // Merge pull request #740
                $@"Merge pull request ({prNumber})",
                $@"HEAD is now at .* Auto-completed PR #({prNumber})" // Tu caso específico
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(logContent, pattern, RegexOptions.IgnoreCase);
                if (match.Success && match.Groups.Count > 1)
                {
                    return match.Groups[1].Value;
                }
            }
            return null;
        }
        private async Task<BuildInfo?> GetLatestBuildAsync(string sourceRefName, int definitionId)
        {
            var _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var baseUrl = Ctx(ProjectType).BaseUrl;
            var url = $"{baseUrl}/_apis/build/builds" +
                     $"?definitions={definitionId}" +
                     $"&branchName={sourceRefName}" +
                     $"&$top=1" +
                     $"&$orderBy=queueTime desc" +
                     $"&api-version=6.0";

            var response = await _httpConfigured.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            var buildsResponse = JsonSerializer.Deserialize<BuildsResponse>(content, _jsonOptions);

            return buildsResponse?.Value?.FirstOrDefault();
        }
    }
}
