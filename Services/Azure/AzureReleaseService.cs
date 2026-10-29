using Newtonsoft.Json.Linq;
using PasadorScript.DbModels;
using PasadorScript.DTO.Auditoria;
using PasadorScript.DTO.AzureDev;
using PasadorScript.Helpers;
using PasadorScript.Models.Reports;
using PasadorScript.Services.Graph;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PasadorScript.Services.Azure
{
    public class AzureReleaseService : AzureBaseService, IAzureReleaseService
    {
        private readonly IAzurePullRequestService _azurePullRequestService;
        private readonly IAzureBuildService _azureBuildService;
        private readonly AzureHttpClientConfigured _httpConfigured;
        private readonly AzureHttpClientUser _httpUser;
        private readonly ILogger<AzureReleaseService> _logger;

        public AzureReleaseService(ILogger<AzureReleaseService> logger, IConfiguration configuration,
            IAzurePullRequestService azurePullRequestService, IAzureBuildService azureBuildService,
            AzureHttpClientConfigured azureHttpClientConfigured, AzureHttpClientUser httpUser,
            AzureHttpClientLegacy azureHttpClientLegacy) : base(configuration)
        {
            _azurePullRequestService = azurePullRequestService;
            _azureBuildService = azureBuildService;
            _httpConfigured = azureHttpClientConfigured;
            _httpUser = httpUser;
            _logger = logger;
        }

        public async Task<bool> HasDeployedInAnyStageAsync(int pullRequestId)
        {
            try
            {
                // Obtener el release asociado al PR
                var release = await ObtenerReleaseDesdePullRequestAsync(pullRequestId);
                if (release == null) return false;

                // Obtener los entornos del release (con expand)
                var baseUrl = Ctx(ProjectType).VsrmUrl;
                var url = $"{baseUrl}/_apis/release/releases/{release.Id}?api-version=6.0&$expand=environments";
                var response = await _httpConfigured.GetAsync(url);
                if (!response.IsSuccessStatusCode) return false;
                var json = await response.Content.ReadAsStringAsync();
                var data = JObject.Parse(json);
                var environments = data["environments"] as JArray;

                // Considerar que está desplegado si algún entorno tiene status "succeeded", "inProgress" o "queued"
                return environments.Any(e =>
                    e["status"]?.ToString() == "succeeded" ||
                    e["status"]?.ToString() == "inProgress" ||
                    e["status"]?.ToString() == "queued"
                );
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> HasDeployedInProdAsync(int pullRequestId)
        {
            try
            {
                // ObtenerReleaseDesdePullRequestAsync ya hidrata Environments vía ObtenerReleaseAsync
                var release = await ObtenerReleaseDesdePullRequestAsync(pullRequestId);
                if (release?.Environments == null || release.Environments.Count == 0)
                    return false;

                return release.Environments.Any(e =>
                    IsProductionEnvironment(e.Name) &&
                    IsProdBlockingStatus(e.Status));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error evaluando deploy PROD para PR {PrId}", pullRequestId);
                return false;
            }
        }

        private static bool IsProductionEnvironment(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            return EnvironmentDto.EsNombreProduccion(name);
        }

        private static bool IsProdBlockingStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return false;

            // Solo bloquea anular si PROD ya se aplicó. Pending / queued / inProgress
            // (pendiente de entrar) no traba: se puede anular y abandonar el Release.
            return status.Equals("succeeded", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<ReleaseEnvironmentActionResult> ActualizarEstadoEnvironmentAsync(int releaseId, int environmentId, ReleaseEnvironmentAction action, string comentario = "")
        {
            var baseUrl = Ctx(ProjectType).VsrmUrl;
            var url = $"{baseUrl}/_apis/release/releases/{releaseId}/environments/{environmentId}?api-version=7.1-preview.4";

            var status = action switch
            {
                ReleaseEnvironmentAction.Approve => "approved",
                ReleaseEnvironmentAction.Reject => "rejected",
                _ => throw new ArgumentOutOfRangeException(nameof(action))
            };

            var payload = new
            {
                status,
                comments = comentario
            };

            var response = await _httpUser.SendAsync(HttpMethod.Patch, url, payload);
            var result = new ReleaseEnvironmentActionResult
            {
                StatusCode = (int)response.StatusCode,
                Success = response.IsSuccessStatusCode
            };

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();

                try
                {
                    // Azure DevOps error format: { "message": "...", "typeName": "...", ... }
                    using var doc = JsonDocument.Parse(errorContent);
                    if (doc.RootElement.TryGetProperty("message", out var message))
                    {
                        result.ErrorMessage = message.GetString();
                    }
                    else
                    {
                        result.ErrorMessage = errorContent;
                    }
                }
                catch
                {
                    result.ErrorMessage = errorContent;
                }
            }

            return result;
        }

        public async Task<ReleaseEnvironmentActionResult> ActualizarEstadoEnvironmentAsync(
            int approvalId,
            ReleaseEnvironmentAction action,
            string comentario = "",
            bool useSystemPat = false)
        {
            var baseUrl = Ctx(ProjectType).VsrmUrl;
            var url = $"{baseUrl}/_apis/release/approvals/{approvalId}?api-version=6.0";

            var payload = new
            {
                status = action == ReleaseEnvironmentAction.Approve ? "approved" : "rejected",
                comments = string.IsNullOrWhiteSpace(comentario)
                    ? action == ReleaseEnvironmentAction.Approve ? "Aprobado sistema" : "Rechazado sistema"
                    : comentario
            };

            var response = await SendReleaseAsync(HttpMethod.Patch, url, payload, useSystemPat);
            var result = new ReleaseEnvironmentActionResult
            {
                StatusCode = (int)response.StatusCode,
                Success = response.IsSuccessStatusCode
            };

            if (!response.IsSuccessStatusCode)
            {
                var errorDetail = await response.Content.ReadAsStringAsync();
                result.ErrorMessage = $"{errorDetail}";
            }
            return result;
        }

        public async Task<bool> CancelarEnvironmentAsync(
            int releaseId,
            int environmentId,
            string? comment = null,
            bool useSystemPat = false)
        {
            if (releaseId <= 0 || environmentId <= 0)
            {
                _logger.LogWarning(
                    "IDs inválidos para cancelar environment. ReleaseId {ReleaseId}, EnvironmentId {EnvironmentId}",
                    releaseId, environmentId);
                return false;
            }

            try
            {
                var url = $"{Ctx(ProjectType).VsrmUrl}/_apis/Release/releases/{releaseId}/environments/{environmentId}?api-version=7.1";
                var requestBody = new
                {
                    status = "canceled",
                    comment = comment ?? "Cancelado desde PasadorScript"
                };

                var response = await SendReleaseAsync(HttpMethod.Patch, url, requestBody, useSystemPat);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation(
                        "Environment {EnvironmentId} del release {ReleaseId} cancelado",
                        environmentId, releaseId);
                    return true;
                }

                var statusCode = (int)response.StatusCode;
                var errorContent = await response.Content.ReadAsStringAsync();
                if (EsStatusNoCancelable(statusCode) && EsErrorEstadoTerminal(errorContent))
                {
                    _logger.LogInformation(
                        "Environment {EnvironmentId} del release {ReleaseId} ya no admite cancelación ({StatusCode})",
                        environmentId, releaseId, statusCode);
                    return true;
                }

                _logger.LogError(
                    "Error al cancelar environment {EnvironmentId} del release {ReleaseId}: {StatusCode}",
                    environmentId, releaseId, response.StatusCode);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excepción al cancelar environment {EnvironmentId} del release {ReleaseId}",
                    environmentId, releaseId);
                return false;
            }
        }

        public async Task<bool> AbandonarReleaseAsync(int releaseId, string? comment = null, bool useSystemPat = false)
        {
            if (releaseId <= 0)
            {
                _logger.LogWarning("ReleaseId inválido para abandonar release: {ReleaseId}", releaseId);
                return false;
            }

            var commentText = comment ?? "Anulado por el usuario";
            var url = $"{Ctx(ProjectType).VsrmUrl}/_apis/release/releases/{releaseId}?api-version=7.1";

            try
            {
                if (await TryAbandonarReleaseAsync(url, releaseId, commentText, useSystemPat))
                    return true;

                if (!useSystemPat && await TryAbandonarReleaseAsync(url, releaseId, commentText, useSystemPat: true))
                    return true;

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excepción al abandonar release {ReleaseId}", releaseId);
                return false;
            }
        }

        private async Task<bool> TryAbandonarReleaseAsync(string url, int releaseId, string comment, bool useSystemPat)
        {
            var patchBody = new { status = "abandoned", comment };
            var patchResponse = await SendReleaseAsync(HttpMethod.Patch, url, patchBody, useSystemPat);
            if (patchResponse.IsSuccessStatusCode)
            {
                _logger.LogInformation("Release {ReleaseId} abandonado", releaseId);
                return true;
            }

            _logger.LogInformation(
                "PATCH mínimo no abandonó release {ReleaseId} ({StatusCode}); se intenta GET+PUT",
                releaseId, (int)patchResponse.StatusCode);

            return await AbandonarReleaseConGetPutAsync(url, releaseId, comment, useSystemPat);
        }

        private async Task<bool> AbandonarReleaseConGetPutAsync(string url, int releaseId, string comment, bool useSystemPat)
        {
            try
            {
                var getResponse = await _httpConfigured.GetAsync(url);
                var json = await getResponse.Content.ReadAsStringAsync();
                var node = JsonNode.Parse(json);
                if (node is null)
                {
                    _logger.LogWarning("No se pudo parsear el release {ReleaseId} para abandonarlo", releaseId);
                    return false;
                }

                node["status"] = "abandoned";
                node["comment"] = comment;

                var putResponse = await SendReleaseAsync(HttpMethod.Put, url, node, useSystemPat);
                if (putResponse.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Release {ReleaseId} abandonado vía GET+PUT", releaseId);
                    return true;
                }

                _logger.LogError(
                    "Error al abandonar release {ReleaseId} vía PUT: {StatusCode}",
                    releaseId, putResponse.StatusCode);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excepción en GET+PUT al abandonar release {ReleaseId}", releaseId);
                return false;
            }
        }

        private Task<HttpResponseMessage> SendReleaseAsync<T>(HttpMethod method, string url, T body, bool useSystemPat) =>
            useSystemPat
                ? _httpConfigured.SendAsync(method, url, body)
                : _httpUser.SendAsync(method, url, body);

        private static bool EsStatusNoCancelable(int statusCode) =>
            statusCode is 400 or 409;

        private static bool EsErrorEstadoTerminal(string? errorContent)
        {
            if (string.IsNullOrWhiteSpace(errorContent))
                return false;

            var lower = errorContent.ToLowerInvariant();
            return lower.Contains("invalid", StringComparison.Ordinal)
                || lower.Contains("not valid", StringComparison.Ordinal)
                || lower.Contains("already", StringComparison.Ordinal)
                || lower.Contains("cannot be changed", StringComparison.Ordinal)
                || lower.Contains("canceled", StringComparison.Ordinal)
                || lower.Contains("cancelled", StringComparison.Ordinal);
        }

        public async Task<string?> DownloadReleaseLogsAsync(int releaseId, string taskName = "")
        {
            var url = $"{Ctx(ProjectType).VsrmUrl}/_apis/release" +
                      $"/releases/{releaseId}/logs?api-version=7.1";

            using var resp = await _httpConfigured.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return null;

            await using var ms = new MemoryStream(await resp.Content.ReadAsByteArrayAsync());
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var res = "";
            foreach (var entry in zip.Entries.Where(e => e.FullName.EndsWith(".log") && (string.IsNullOrEmpty(taskName) || e.Name.Contains(taskName))))
            {
                await using var s = entry.Open();
                using var sr = new StreamReader(s);
                var texto = await sr.ReadToEndAsync();

                res += $"=== {entry.Name} ===\n";
                res += $"{texto}\n";
            }
            return res;
        }

        public async Task<List<EnvironmentDto>> GetReleaseDefinitionEnvironmentsAsync(int releaseDefinitionId)
        {
            try
            {
                //var baseUrl = GetUriFormattedVSRM("BaseUrl");
                var baseUrl = Ctx(ProjectType).VsrmUrl;
                var url = $"{baseUrl}/_apis/release/definitions/{releaseDefinitionId}?api-version=7.1";

                var response = await _httpConfigured.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Error al obtener el release definition: {response.StatusCode} - {response.ReasonPhrase}");
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var definitionResponse = JsonSerializer.Deserialize<ReleaseDefinitionResponse>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return definitionResponse?.Environments?.Select(env => new EnvironmentDto
                {
                    Id = env.Id,
                    Name = env.Name
                }).ToList() ?? new List<EnvironmentDto>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al obtener environments del release definition {releaseDefinitionId}: {ex.Message}", ex);
            }
        }

        public async Task<List<ReleaseDefinitionInfo>> GetReleaseDefinitionsAsync()
        {
            try
            {
                //var baseUrl = GetUriFormattedVSRM("BaseUrl");
                var baseUrl = Ctx(ProjectType).VsrmUrl;
                var url = $"{baseUrl}/_apis/release/definitions?api-version=7.1";

                var response = await _httpConfigured.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Error al obtener release definitions: {response.StatusCode} - {response.ReasonPhrase}");
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var definitionsResponse = JsonSerializer.Deserialize<ReleaseDefinitionsResponse>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return definitionsResponse?.Value?.Select(def => new ReleaseDefinitionInfo
                {
                    Id = def.Id,
                    Name = def.Name,
                    Path = def.Path
                }).ToList() ?? new List<ReleaseDefinitionInfo>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al obtener release definitions del proyecto {Ctx(ProjectType).Project}: {ex.Message}", ex);
            }
        }

        public async Task<List<ReleaseDefinitionInfo>> GetReleaseDefinitionsAsync(string repositoryId, string repositoryName)
        {
            // ---------- 1) Build pipelines que usan el repo ----------
            var json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var baseUrl = Ctx(ProjectType).BaseUrl;
            var buildUrl =
                $"{baseUrl}/_apis/build/definitions" +
                $"?repositoryId={repositoryId}&repositoryType=TfsGit&name={repositoryName}&api-version=7.1-preview.7";
            var resp = await _httpConfigured.GetAsync(buildUrl);
            var buildsJson = await resp.Content.ReadAsStringAsync();
            var buildList = JsonSerializer.Deserialize<BuildDefList>(buildsJson, json)
                              ?? new BuildDefList();
            var projectGuid = buildList.Value.FirstOrDefault()?.Project.Id;

            //await ObtenerReleasePorBuildIdAsync(projectGuid, buildList.Value.First()!.Id!);

            var releases = new List<ReleaseDef>();
            List<ReleaseDefinitionInfo> res = new List<ReleaseDefinitionInfo>();
            // ---------- 2) Releases cuyo artefacto = Build ----------
            var vsrmBaseUrl = Ctx(ProjectType).VsrmUrl;
            foreach (var b in buildList.Value)
            {
                var relUrl =
                    $"{vsrmBaseUrl}/_apis/release/definitions" +
                    $"?artifactType=Build&artifactSourceId={projectGuid}:{b.Id}" +
                    $"&$expand=artifacts&api-version=7.1-preview.4";
                var respJson = await _httpConfigured.GetAsync(relUrl);
                var relJson = await respJson.Content.ReadAsStringAsync();
                var definitionsResponse = JsonSerializer.Deserialize<ReleaseDefinitionsResponse>(relJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                res = definitionsResponse?.Value?.Select(def => new ReleaseDefinitionInfo
                {
                    Id = def.Id,
                    Name = def.Name,
                    Path = def.Path
                }).ToList() ?? new List<ReleaseDefinitionInfo>();
            }
            return res;

        }

        public async Task<string?> GetReleaseTaskLogAsync(int releaseId, int environmentId, int taskId)
        {
            //var _baseUrl = GetUriFormattedVSRM("BaseUrl");
            var _baseUrl = Ctx(ProjectType).VsrmUrl;
            try
            {
                // Para obtener logs de tareas específicas, necesitamos usar un enfoque diferente
                // Ya que el endpoint directo de deployment logs no existe
                // 3️⃣ Obtener log de la task
                var logUrl = $"{_baseUrl}/_apis/release/releases/{releaseId}/environments/{environmentId}/tasks/{taskId}/logs?api-version=7.1-preview.2";

                // Opción 1: Intentar con el endpoint de logs del release completo
                // var releaseLogsUrl = $"{_baseUrl}/_apis/release/releases/{releaseId}/logs?api-version=7.0";

                try
                {
                    var resp = await _httpConfigured.GetAsync(logUrl);
                    var logText = await resp.Content.ReadAsStringAsync();
                    //var logsResponse = await _httpClient.GetStringAsync(releaseLogsUrl);

                    // El log del release completo incluye todas las tareas
                    // Necesitamos filtrar por la tarea específica
                    return ExtractTaskLogFromFullLog(logText, taskId);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not get release logs from primary endpoint");
                }

                // Opción 2: Usar el método que ya tienes implementado
                // Tu método DownloadReleaseLogsAsync probablemente ya funciona
                var fullLog = await DownloadReleaseLogsAsync(releaseId);
                if (!string.IsNullOrEmpty(fullLog))
                {
                    return ExtractTaskLogFromFullLog(fullLog, taskId);
                }

                return $"No se pudo obtener el log para la tarea {taskId}";
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, "Error getting task log for release {ReleaseId}, task {TaskId}",
                //    releaseId, taskId);
                return $"Error al obtener el log: {ex.Message}";
            }
        }

        public async Task<ReleaseTasksAndLogsDto?> GetReleaseTasksAndLogsAsync(int releaseId, int environmentId)
        {
            var _baseUrl = Ctx(ProjectType).VsrmUrl;
            try
            {
                // 1. Obtener información del release (vsrm domain)
                var releaseUrl = $"{_baseUrl}/_apis/release/releases/{releaseId}?api-version=7.0";
                var responseRel = await _httpConfigured.GetAsync(releaseUrl);
                var releaseResponse = await responseRel.Content.ReadAsStringAsync();
                var releaseData = JsonSerializer.Deserialize<JsonElement>(releaseResponse);

                var releaseName = releaseData.GetProperty("name").GetString();
                var environments = releaseData.GetProperty("environments").EnumerateArray();

                var environment = environments.FirstOrDefault(e =>
                    e.GetProperty("id").GetInt32() == environmentId);

                if (environment.ValueKind == JsonValueKind.Undefined)
                    return null;

                var environmentName = environment.GetProperty("name").GetString();

                // 2. Obtener deployment attempts para este environment
                var deploymentAttempts = environment.GetProperty("deploySteps").EnumerateArray();
                var tasks = new List<ReleaseTaskDto>();

                foreach (var deployStep in deploymentAttempts)
                {
                    if (deployStep.TryGetProperty("releaseDeployPhases", out var phasesElement))
                    {
                        var phaseOrder = 1;
                        foreach (var phase in phasesElement.EnumerateArray())
                        {
                            var phaseName = phase.GetProperty("name").GetString();

                            if (phase.TryGetProperty("deploymentJobs", out var jobsElement))
                            {
                                foreach (var job in jobsElement.EnumerateArray())
                                {
                                    if (job.TryGetProperty("tasks", out var tasksElement))
                                    {
                                        var taskOrder = 1;
                                        foreach (var task in tasksElement.EnumerateArray())
                                        {
                                            var taskDto = new ReleaseTaskDto
                                            {
                                                Id = task.GetProperty("id").GetInt32(),
                                                Name = $"{task.GetProperty("name").GetString()}",
                                                Status = task.GetProperty("status").GetString()?.ToLower() ?? "unknown",
                                                Order = (phaseOrder * 1000) + taskOrder, // Para mantener orden por fases
                                                HasLog = HasTaskLog(task)
                                            };

                                            // Obtener timestamps
                                            if (task.TryGetProperty("startTime", out var startTimeElement) &&
                                                !string.IsNullOrEmpty(startTimeElement.GetString()))
                                            {
                                                if (DateTime.TryParse(startTimeElement.GetString(), out var startTime))
                                                    taskDto.StartTime = startTime;
                                            }

                                            if (task.TryGetProperty("finishTime", out var finishTimeElement) &&
                                                !string.IsNullOrEmpty(finishTimeElement.GetString()))
                                            {
                                                if (DateTime.TryParse(finishTimeElement.GetString(), out var finishTime))
                                                    taskDto.FinishTime = finishTime;
                                            }

                                            tasks.Add(taskDto);
                                            taskOrder++;
                                        }
                                    }
                                }
                            }
                            phaseOrder++;
                        }
                    }
                }

                return new ReleaseTasksAndLogsDto
                {
                    ReleaseId = releaseId,
                    ReleaseName = releaseName ?? $"Release {releaseId}",
                    EnvironmentId = environmentId,
                    EnvironmentName = environmentName ?? "Unknown Environment",
                    Tasks = tasks.OrderBy(t => t.Order).ToList()
                };
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, "Error getting release tasks for release {ReleaseId}, environment {EnvironmentId}",
                //    releaseId, environmentId);
                return null;
            }
        }

        public async Task<ReleaseAuditDto> ObtenerAuditoriaReleaseAsync(int releaseId, int pullRequestId)
        {
            string baseUrl = Ctx(ProjectType).VsrmUrl;
            var url = $"{baseUrl}/_apis/release/releases/{releaseId}?api-version=7.1-preview.8";

            var response = await _httpConfigured.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Error obteniendo release: {response.StatusCode}");
            }

            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            var audit = new ReleaseAuditDto
            {
                ReleaseId = releaseId,
                ReleaseName = root.GetProperty("name").GetString()!,
                CreatedOn = root.GetProperty("createdOn").GetDateTime()
            };

            // Obtener información del Pull Request
            audit.PullRequest = await ObtenerPullRequestAuditInfoAsync(pullRequestId);

            // Procesar environments
            var allEnvironments = root.GetProperty("environments").EnumerateArray().ToList();
            var environmentAttempts = new Dictionary<int, int>();

            // Detectar attempts
            foreach (var env in allEnvironments)
            {
                var envId = env.GetProperty("id").GetInt32();
                int maxAttempt = GetMaxAttemptFromApprovals(env);
                environmentAttempts[envId] = maxAttempt;
            }

            // Agrupar por definitionEnvironmentId y obtener todos los attempts
            var environmentGroups = allEnvironments
                .GroupBy(env => env.GetProperty("definitionEnvironmentId").GetInt32())
                .ToList();

            foreach (var group in environmentGroups)
            {
                // Obtener todos los attempts ordenados
                var attempts = group
                    .OrderBy(env => environmentAttempts[env.GetProperty("id").GetInt32()])
                    .ToList();

                foreach (var env in attempts)
                {
                    var envId = env.GetProperty("id").GetInt32();
                    var currentAttempt = environmentAttempts[envId];

                    var envAudit = new EnvironmentAuditDto
                    {
                        EnvironmentId = envId,
                        Name = env.GetProperty("name").GetString()!,
                        DefinitionEnvironmentId = env.GetProperty("definitionEnvironmentId").GetInt32(),
                        Attempt = currentAttempt,
                        Status = env.GetProperty("status").GetString()!
                    };

                    // Deployment Info
                    envAudit.Deployment = ExtractDeploymentInfo(env);

                    // Pre-deploy approvals
                    if (env.TryGetProperty("preDeployApprovals", out var preApprovals) &&
                        preApprovals.ValueKind == JsonValueKind.Array)
                    {
                        ProcessAuditApprovals(preApprovals, envAudit.PreDeployApprovals, currentAttempt);
                    }

                    // Post-deploy approvals
                    if (env.TryGetProperty("postDeployApprovals", out var postApprovals) &&
                        postApprovals.ValueKind == JsonValueKind.Array)
                    {
                        ProcessAuditApprovals(postApprovals, envAudit.PostDeployApprovals, currentAttempt);
                    }

                    audit.Environments.Add(envAudit);
                }
            }

            return audit;
        }

        public async Task<int?> ObtenerDeploymentPorEnvironmentAsync(int releaseId, string environmentName, bool soloActivos = true)
        {
            var baseUrl = Ctx(ProjectType).VsrmUrl;
            var url = $"{baseUrl}/_apis/release/deployments?releaseId={releaseId}&$top=100&api-version=7.1-preview.1";

            var response = await _httpConfigured.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Error al obtener deployments: {response.StatusCode} - {error}");
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var deployments = doc.RootElement.GetProperty("value");

            int? lastDeploymentId = null;
            DateTime? lastQueuedOn = null;

            foreach (var deployment in deployments.EnumerateArray())
            {
                var env = deployment.GetProperty("releaseEnvironment");
                var nombreEnv = env.GetProperty("name").GetString();
                var status = deployment.GetProperty("deploymentStatus").GetString();

                if (nombreEnv!.Equals(environmentName, StringComparison.OrdinalIgnoreCase) &&
                    (!soloActivos || status == "inProgress"))
                {
                    var queuedOn = deployment.GetProperty("queuedOn").GetDateTime();

                    if (!lastQueuedOn.HasValue || queuedOn > lastQueuedOn.Value)
                    {
                        lastDeploymentId = deployment.GetProperty("id").GetInt32();
                        lastQueuedOn = queuedOn;
                    }
                }
            }

            return lastDeploymentId;
        }

        public async Task<PRReleaseStatus> ObtenerEstadoReleasePorPRCompletadoAsync(int prId)
        {
            // Verificar que el PR esté completado
            var prInfo = await _azurePullRequestService.ObtenerInfoPullRequestAsync(prId);
            if (prInfo == null)
            {
                return new PRReleaseStatus { Message = "PR no encontrado" };
            }

            if (prInfo.Status != "completed")
            {
                return new PRReleaseStatus
                {
                    HasBuild = false,
                    HasRelease = false,
                    Message = $"PR está en estado '{prInfo.Status}', no completado"
                };
            }

            // Buscar el build del merge commit
            var buildInfo = await ObtenerBuildPorMergeCommitAsync(prId);
            if (buildInfo == null)
            {
                return new PRReleaseStatus
                {
                    HasBuild = false,
                    HasRelease = false,
                    Message = "No se encontró build para el merge commit del PR"
                };
            }

            // Buscar release asociada al build
            var releaseInfo = await ObtenerReleasePorBuildAsync(buildInfo.Id);
            if (releaseInfo == null)
            {
                return new PRReleaseStatus
                {
                    HasBuild = true,
                    HasRelease = false,
                    BuildId = buildInfo.Id,
                    Message = "Build encontrado pero no hay release iniciada"
                };
            }

            return new PRReleaseStatus
            {
                HasBuild = true,
                HasRelease = true,
                BuildId = buildInfo.Id,
                ReleaseId = releaseInfo.Id,
                ReleaseStatus = releaseInfo.Status,
                ReleaseName = releaseInfo.Name,
                Message = $"Release '{releaseInfo.Name}' encontrada con estado: {releaseInfo.Status}"
            };
        }

        public async Task<ReleaseDto?> ObtenerReleaseAsync(int releaseId)
        {
            string baseUrl = Ctx(ProjectType).VsrmUrl;
            var url = $"{baseUrl}/_apis/release/releases/{releaseId}?api-version=7.1-preview.8";

            var response = await _httpConfigured.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Error: {response.StatusCode}");
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            var allEnvironments = root.GetProperty("environments").EnumerateArray().ToList();

            // Crear un diccionario para almacenar el attempt máximo de cada environment
            var environmentAttempts = new Dictionary<int, int>();

            foreach (var env in allEnvironments)
            {
                var envId = env.GetProperty("id").GetInt32();
                var defEnvId = env.GetProperty("definitionEnvironmentId").GetInt32();
                var name = env.GetProperty("name").GetString();
                var status = env.GetProperty("status").GetString();

                // Detectar el attempt máximo mirando los approvals
                int maxAttempt = GetMaxAttemptFromApprovals(env);
                environmentAttempts[envId] = maxAttempt;

                DateTime? modifiedOn = null;
                if (env.TryGetProperty("modifiedOn", out var modProp))
                {
                    modifiedOn = modProp.GetDateTime();
                }

            }

            // Seleccionar los environments más recientes por definitionEnvironmentId
            var latestAttempts = allEnvironments
                .GroupBy(env => env.GetProperty("definitionEnvironmentId").GetInt32())
                .Select(group =>
                {
                    // Ordenar por attempt detectado (máximo de los approvals)
                    return group
                        .OrderByDescending(env => environmentAttempts[env.GetProperty("id").GetInt32()])
                        .ThenByDescending(env =>
                            env.TryGetProperty("modifiedOn", out var modProp) ?
                            modProp.GetDateTime() : DateTime.MinValue)
                        .ThenByDescending(env => env.GetProperty("id").GetInt32())
                        .First();
                })
                .ToList();

            foreach (var env in latestAttempts)
            {
                var envId = env.GetProperty("id").GetInt32();
                var defEnvId = env.GetProperty("definitionEnvironmentId").GetInt32();
                var name = env.GetProperty("name").GetString();
                var status = env.GetProperty("status").GetString();
                var detectedAttempt = environmentAttempts[envId];

            }

            var release = new ReleaseDto
            {
                Id = root.GetProperty("id").GetInt32(),
                Name = root.GetProperty("name").GetString()!,
                Status = root.GetProperty("status").GetString()!,
                CreatedBy = JsonSerializer.Deserialize<CreatedBy>(root.GetProperty("createdBy").ToString(), new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                })!,
                ModifiedOn = root.GetProperty("modifiedOn").GetDateTime(),
                CreatedOn = root.GetProperty("createdOn").GetDateTime(),
                Url = root.GetProperty("url").GetString()!
            };

            foreach (var env in latestAttempts)
            {
                var envId = env.GetProperty("id").GetInt32();
                var currentAttempt = environmentAttempts[envId];
                ProcessEnvironment(env, release, currentAttempt);
            }

            return release;
        }

        public async Task<ReleaseDto?> ObtenerReleasePendientesAsync(int definitionId)
        {
            var projectId = await ObtenerProjectIdPorNombreAsync();

            if (projectId == null)
            {
                _logger.LogError("❌ No se pudo obtener el Project ID.");
                return null;
            }
            string filters = "queued,pending";
            return await ObtenerReleasePorBuildIdAsync(projectId, filters, definitionId);
        }

        public async Task<ReleaseDto?> ObtenerReleaseDesdePullRequestAsync(int prId)
        {
            int? definitionId = null;
            var projectId = await ObtenerProjectIdPorNombreAsync();

            BuildInfo? build = await _azureBuildService.GetBuildByPullRequestAsync(prId, definitionId);

            if (build == null)
            {
                _logger.LogDebug("⚠️ No se encontró build asociado al PR.");
                return null;
            }

            if (projectId == null)
            {
                _logger.LogError("❌ No se pudo obtener el Project ID.");
                return null;
            }

            return await ObtenerReleasePorBuildIdAsync(projectId, build.Id, build.DefinitionId);
        }

        public async Task<ReleaseDto?> ObtenerReleaseDesdePullRequestAsync(string projectName, int prId)
        {
            int? definitionId = null;
            var projectId = await ObtenerProjectIdPorNombreAsync(projectName);

            BuildInfo? build = await _azureBuildService.GetBuildByPullRequestAsync(prId, definitionId);

            if (build == null)
            {
                _logger.LogDebug("⚠️ No se encontró build asociado al PR.");
                return null;
            }

            if (projectId == null)
            {
                _logger.LogError("❌ No se pudo obtener el Project ID.");
                return null;
            }

            return await ObtenerReleasePorBuildIdAsync(projectId, build.Id, build.DefinitionId);
        }

        public async Task<bool> RedeployEnvironmentAsync(int releaseId, int environmentId, string? comment = null)
        {
            var baseUrl = Ctx(ProjectType).VsrmUrl;
            //var id = await _serviciosGraph.GetMyIdAsAsync();

            var body = new
            {
                status = "inProgress",
                comment = comment,
                variables = new { },
                scheduledDeploymentTime = (string?)null,
                environmentOptions = new
                {
                    emailNotificationType = "OnlyOnFailure",
                    emailRecipients = "",
                    skipArtifactsDownload = false,
                    timeoutInMinutes = 0,
                    enableAccessToken = false,
                    publishDeploymentStatus = true,
                    badgeEnabled = false,
                    autoLinkWorkItems = false,
                    pullRequestDeploymentEnabled = false
                },
                demands = Array.Empty<object>(),
                conditions = Array.Empty<object>(),
                rank = 1,
                runOptions = new { },
                variablesToReset = Array.Empty<string>()
            };

            var url = $"{baseUrl}/_apis/Release" +
                      $"/releases/{releaseId}/environments/{environmentId}?api-version=7.1";

            var response = await _httpUser.SendAsync(HttpMethod.Patch, url, body);

            return response.IsSuccessStatusCode;
        }

        public async Task<List<ReleaseWithPullRequestInfo>> GetPendingReleasesWithPullRequestInfo(int definitionId, string? statusFilter)
        {
            var baseUrl = Ctx(ProjectType).VsrmUrl;
            var result = new List<ReleaseWithPullRequestInfo>();

            string releasesUrl = $"{baseUrl}/_apis/release/releases" +
             $"?definitionId={definitionId}" +  // Si quieres filtrar por definición específica
             $"&$expand=environments,artifacts" +
             $"&statusFilter={statusFilter}" +  // Filtro por estados pendientes
             $"&queryOrder=descending" +
             $"&api-version=7.1-preview.8";

            var releasesResponse = await _httpConfigured.GetAsync(releasesUrl);
            var releasesContent = await releasesResponse.Content.ReadAsStringAsync();
            var releases = JObject.Parse(releasesContent)["value"] as JArray;
            var filterArray = statusFilter.Split(",");

            foreach (var release in releases)
            {
                var environments = release["environments"] as JArray;
                var pendingEnvironments = new List<JToken>();

                // 2. Filtrar ambientes en estado queued/pending
                foreach (var env in environments)
                {
                    var envStatus = env["status"]?.ToString();
                    if (filterArray.Contains(envStatus))
                    {
                        pendingEnvironments.Add(env);
                    }
                }

                if (pendingEnvironments.Any())
                {

                    var releaseInfo = new ReleaseWithPullRequestInfo
                    {
                        ReleaseId = release["id"].Value<int>(),
                        ReleaseName = release["name"].ToString(),
                        Status = release["status"].ToString()
                    };

                    // 2. Buscar artefacto de build
                    var artifacts = release["artifacts"] as JArray;
                    foreach (var artifact in artifacts)
                    {
                        if (artifact["type"]?.ToString() == "Build")
                        {
                            var buildId = artifact["definitionReference"]?["version"]?["id"]?.ToString();
                            var repositoryId = artifact["definitionReference"]?["repository"]?["id"]?.ToString();

                            if (!string.IsNullOrEmpty(buildId))
                            {
                                // 3. Obtener detalles del build
                                string buildUrl = $"{Ctx(ProjectType).BaseUrl}/_apis/build/builds/{buildId}?api-version=7.1";
                                var buildResponse = await _httpConfigured.GetAsync(buildUrl);
                                var buildContent = await buildResponse.Content.ReadAsStringAsync();
                                var build = JObject.Parse(buildContent);

                                // 4. Buscar PR ID en triggerInfo
                                var triggerInfo = build["triggerInfo"];
                                int? prId = null;
                                if (triggerInfo != null)
                                {
                                    var message = triggerInfo["ci.message"]!;

                                    string? sourceBranch = triggerInfo["ci.sourceBranch"]?.ToString();
                                    if (message != null)
                                        prId = ExtractPullRequestNumber(message.ToString());

                                    releaseInfo.PullRequestId = prId;
                                    releaseInfo.PullRequestSourceBranch = triggerInfo["pr.sourceBranch"]?.ToString();
                                }

                                // 5. Si no encontró PR ID, guarda al menos el commit
                                if (prId.HasValue)
                                {
                                    releaseInfo.SourceCommitId = build["sourceVersion"]?.ToString();
                                }

                                releaseInfo.BuildId = buildId;
                                releaseInfo.RepositoryId = repositoryId;
                            }
                            break;
                        }
                    }

                    result.Add(releaseInfo);
                }
            }

            return result;
        }

        private int? ExtractPullRequestNumber(string prDescription)
        {
            if (string.IsNullOrEmpty(prDescription))
                return null;

            // Patrones comunes para PR numbers
            string[] patterns = {
                @"#(\d+)",                    // #3842
                @"PR\s*#?(\d+)",              // PR 3842 o PR#3842
                @"pull request\s*#?(\d+)",     // pull request 3842 o pull request#3842
                @"(\d+)"                       // Cualquier número (menos específico)
            };

            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(prDescription, pattern, RegexOptions.IgnoreCase);
                if (match.Success && match.Groups.Count > 1)
                {
                    if (int.TryParse(match.Groups[1].Value, out int prNumber))
                        return prNumber;
                }
            }

            return null;
        }

        private async Task<ReleaseDto?> ObtenerReleasePorBuildIdAsync(string projectId, string statusFilter, int? definitionId = null)
        {
            var baseUrl = Ctx(ProjectType).VsrmUrl;

            string url = $"{baseUrl}/_apis/release/releases" +
             $"?definitionId={definitionId}" +  // Si quieres filtrar por definición específica
             $"&$expand=environments" +
             $"&statusFilter={statusFilter}" +  // Filtro por estados pendientes
             $"&queryOrder=descending" +
             $"&api-version=7.1-preview.8";

            var response = await _httpConfigured.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            var releases = doc.RootElement.GetProperty("value");

            var firstRelease = releases.EnumerateArray().FirstOrDefault();
            if (firstRelease.ValueKind == JsonValueKind.Undefined) return null;

            int releaseId = firstRelease.GetProperty("id").GetInt32();
            return await ObtenerReleaseAsync(releaseId);
        }

        //Helpers
        private async Task<ReleaseDto?> ObtenerReleasePorBuildIdAsync(string projectId, int buildId, int? definitionId = null)
        {
            var baseUrl = Ctx(ProjectType).VsrmUrl;
            if (definitionId == null)
                //definitionId = GetDefinitionIdForCompiledGX();
                definitionId = await _azureBuildService.GetDefinitionIdByBuildId(buildId);
            var url = "";
            if (definitionId == null) //sigue siendo Null, es un script
            {
                url = $"{baseUrl}/_apis/release/releases" +
              $"?artifactType=Build&artifactSourceId={projectId}:{buildId}" +
              $"&$expand=environments&queryOrder=descending&$top=5&api-version=7.1-preview.8";
            }
            else
            {
                var sourceId = $"{projectId}:{definitionId}";
                url = $"{baseUrl}/_apis/release/releases" +
                          $"?sourceId={sourceId}" +
                          $"&artifactVersionId={buildId}" +
                          $"&api-version=7.1-preview.8";
            }
            var response = await _httpConfigured.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            var releases = doc.RootElement.GetProperty("value");

            var firstRelease = releases.EnumerateArray().FirstOrDefault();
            if (firstRelease.ValueKind == JsonValueKind.Undefined) return null;

            int releaseId = firstRelease.GetProperty("id").GetInt32();
            return await ObtenerReleaseAsync(releaseId);
        }
        private async Task<string?> ObtenerProjectIdPorNombreAsync()
        {
            string projectName = Ctx(ProjectType).Project;
            var host = Ctx(ProjectType).AzureDevopsHost;
            var organization = Ctx(ProjectType).Organization;
            // Endpoint
            var url = $"{host}/{organization}/_apis/projects/{projectName}?api-version=7.1-preview.4";
            var response = await _httpConfigured.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            return doc.RootElement.GetProperty("id").GetString();
        }

        private async Task<string?> ObtenerProjectIdPorNombreAsync(string projectName)
        {
            var host = Ctx(projectName).AzureDevopsHost;
            var organization = Ctx(projectName).Organization;
            // Endpoint
            var url = $"{host}/{organization}/_apis/projects/{projectName}?api-version=7.1-preview.4";
            var response = await _httpConfigured.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            return doc.RootElement.GetProperty("id").GetString();
        }

        private void ProcessEnvironment(JsonElement env, ReleaseDto release, int currentAttempt)
        {
            env.TryGetProperty("modifiedOn", out var dt);
            DateTime? dateEnv = dt.ValueKind != JsonValueKind.Undefined ? dt.GetDateTime() : null;

            var envDto = new EnvironmentDto
            {
                definitionEnvironmentId = env.GetProperty("definitionEnvironmentId").GetInt32(),
                Id = env.GetProperty("id").GetInt32(),
                Name = env.GetProperty("name").GetString()!,
                Status = env.GetProperty("status").GetString()!,
                ModifiedOn = dateEnv,
                Attempt = currentAttempt
            };

            // Pre-deploy approvals - FILTRAR POR ATTEMPT
            if (env.TryGetProperty("preDeployApprovals", out var preApprovals) &&
                preApprovals.ValueKind == JsonValueKind.Array)
            {
                ProcessApprovals(preApprovals, envDto.PreDeployApprovals, currentAttempt);
            }

            // Post-deploy approvals - FILTRAR POR ATTEMPT
            if (env.TryGetProperty("postDeployApprovals", out var postApprovals) &&
                postApprovals.ValueKind == JsonValueKind.Array)
            {
                ProcessApprovals(postApprovals, envDto.PostDeployApprovals, currentAttempt);
            }

            // Pre-deploy approvals definition
            if (env.TryGetProperty("preApprovalsSnapshot", out var preObj) &&
                preObj.ValueKind == JsonValueKind.Object &&
                preObj.TryGetProperty("approvals", out var preApprovalsDef) &&
                preApprovalsDef.ValueKind == JsonValueKind.Array)
            {
                ProcessApprovalDefinitions(preApprovalsDef, envDto.PreApprovedDefinition);
            }

            // Post-deploy approvals definition
            if (env.TryGetProperty("postApprovalsSnapshot", out var postObj) &&
                postObj.ValueKind == JsonValueKind.Object &&
                postObj.TryGetProperty("approvals", out var postApprovalsDef) &&
                postApprovalsDef.ValueKind == JsonValueKind.Array)
            {
                ProcessApprovalDefinitions(postApprovalsDef, envDto.PostApprovedDefinition);
            }

            release.Environments.Add(envDto);
        }
        private void ProcessApprovalDefinitions(JsonElement approvalsArray, ICollection<ApprovalDto> targetCollection)
        {
            foreach (var approval in approvalsArray.EnumerateArray())
            {
                if (approval.TryGetProperty("approver", out var approv))
                {
                    var approver = approv.ToString()!;
                    targetCollection.Add(new ApprovalDto
                    {
                        Approver = JsonSerializer.Deserialize<CreatedBy>(approver, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        })!
                    });
                }
            }
        }
        private void ProcessApprovals(JsonElement approvalsArray, ICollection<Approvers> targetCollection, int currentAttempt)
        {

            foreach (var approval in approvalsArray.EnumerateArray())
            {
                // Filtrar por attempt
                int approvalAttempt = 1;
                if (approval.TryGetProperty("attempt", out var attemptProp))
                {
                    approvalAttempt = attemptProp.GetInt32();
                }

                var approvalId = approval.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0;
                var status = approval.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : "unknown";


                // Solo procesar approvals que corresponden al attempt actual
                if (approvalAttempt != currentAttempt)
                {
                    continue;
                }


                if (approval.TryGetProperty("approver", out var approv))
                {
                    var approver = approv.ToString()!;
                    CreatedBy userApprovedBy = null;
                    if (approval.TryGetProperty("approvedBy", out var approvBy))
                    {
                        var approvedBy = approvBy.ToString()!;
                        userApprovedBy = JsonSerializer.Deserialize<CreatedBy>(approvedBy, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        })!;
                    }
                    var id = approval.GetProperty("id").GetInt32();
                    var modifiedOn = approval.GetProperty("modifiedOn").GetDateTime();

                    targetCollection.Add(new Approvers
                    {
                        Approver = JsonSerializer.Deserialize<CreatedBy>(approver, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        })!,
                        ApprovedBy = userApprovedBy!,
                        Status = status!,
                        ModifiedOn = modifiedOn,
                        Id = id
                    });
                }
            }

        }
        private async Task<ReleaseDto?> ObtenerReleasePorBuildAsync(int buildId)
        {
            string baseUrl = Ctx(ProjectType).VsrmUrl;

            // Usar Release Management API - nota el subdominio vsrm
            var url = $"{baseUrl}/_apis/release/releases?sourceBranchFilter=&artifactVersionId={buildId}&$top=1&api-version=7.1-preview.8";

            var response = await _httpConfigured.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            var releases = doc.RootElement.GetProperty("value");

            if (releases.GetArrayLength() == 0) return null;

            var release = releases.EnumerateArray().FirstOrDefault();
            if (release.ValueKind == JsonValueKind.Undefined) return null;

            return new ReleaseDto
            {
                Id = release.GetProperty("id").GetInt32(),
                Name = release.GetProperty("name").GetString() ?? "",
                Status = release.GetProperty("status").GetString() ?? "",
                CreatedOn = release.GetProperty("createdOn").GetDateTime(),
                // = release.GetProperty("releaseDefinition").GetProperty("id").GetInt32()
            };
        }
        private async Task<BuildInfo> ObtenerBuildPorMergeCommitAsync(int prId)
        {
            var prInfo = await _azurePullRequestService.ObtenerInfoPullRequestAsync(prId);
            BuildInfo build = null;

            // Estrategia 1: Buscar por commit del merge
            if (!string.IsNullOrEmpty(prInfo.LastMergeCommit?.CommitId))
            {
                build = await _azureBuildService.BuscarBuildPorCommit(prInfo.LastMergeCommit.CommitId, "completed");
                if (build != null) return build;
            }

            // Estrategia 2: Buscar por source branch
            build = await BuscarBuildPorBranch(prInfo.SourceBranch);
            if (build != null) return build;

            // Estrategia 3: Buscar por target branch
            build = await BuscarBuildPorBranch(prInfo.TargetBranch);
            if (build != null) return build;

            return null;
        }
        private async Task<BuildInfo> BuscarBuildPorBranch(string branchName)
        {
            var _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            string baseUrl = Ctx(ProjectType).BaseUrl;
            // Normalizar el nombre del branch
            var normalizedBranch = branchName.StartsWith("refs/heads/")
                ? branchName
                : $"refs/heads/{branchName}";

            var buildsUrl = $"{baseUrl}/_apis/build/builds?branchName={Uri.EscapeDataString(normalizedBranch)}&statusFilter=completed&queryOrder=finishTimeDescending&$top=20&api-version=7.1-preview.7";

            var response = await _httpConfigured.GetAsync(buildsUrl);
            if (!response.IsSuccessStatusCode) return null;

            var content = await response.Content.ReadAsStringAsync();
            var buildsResponse = JsonSerializer.Deserialize<BuildsResponse>(content, _jsonOptions);

            return buildsResponse?.Value?.FirstOrDefault()!;
        }
        private void ProcessAuditApprovals(JsonElement approvalsArray, List<ApprovalAuditInfo> targetList, int currentAttempt)
        {
            foreach (var approval in approvalsArray.EnumerateArray())
            {
                int approvalAttempt = 1;
                if (approval.TryGetProperty("attempt", out var attemptProp))
                {
                    approvalAttempt = attemptProp.GetInt32();
                }

                if (approvalAttempt != currentAttempt)
                    continue;

                var auditInfo = new ApprovalAuditInfo
                {
                    ApprovalId = approval.GetProperty("id").GetInt32(),
                    Status = approval.GetProperty("status").GetString()!,
                    CreatedOn = approval.GetProperty("createdOn").GetDateTime(),
                    IsAutomated = approval.TryGetProperty("isAutomated", out var isAuto) && isAuto.GetBoolean()
                };

                if (approval.TryGetProperty("modifiedOn", out var modOn))
                {
                    auditInfo.ModifiedOn = modOn.GetDateTime();
                }

                if (approval.TryGetProperty("approver", out var approver))
                {
                    auditInfo.ApprovalFor = JsonSerializer.Deserialize<CreatedBy>(
                        approver.ToString(),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
                }

                if (approval.TryGetProperty("approvedBy", out var approvedBy))
                {
                    auditInfo.ApprovedBy = JsonSerializer.Deserialize<CreatedBy>(
                        approvedBy.ToString(),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
                }

                if (approval.TryGetProperty("comments", out var comments))
                {
                    auditInfo.Comments = comments.GetString()!;
                }

                targetList.Add(auditInfo);
            }
        }
        private bool HasTaskLog(JsonElement task)
        {
            // Verificar si la tarea tiene logs disponibles
            // Generalmente las tareas con ciertos estados tienen logs
            if (task.TryGetProperty("status", out var statusElement))
            {
                var status = statusElement.GetString()?.ToLower();
                return status is "succeeded" or "failed" or "canceled" or "inprogress";
            }
            return false;
        }
        public async Task<PullRequestAuditInfo> ObtenerPullRequestAuditInfoAsync(int pullRequestId)
        {
            // Obtener info del PR
            string baseUrl = Ctx(ProjectType).BaseUrl;
            var prUrl = $"{baseUrl}/_apis/git/pullrequests/{pullRequestId}?api-version=7.0";

            var response = await _httpConfigured.GetAsync(prUrl);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Error obteniendo PR: {response.StatusCode}");
            }

            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            var prInfo = new PullRequestAuditInfo
            {
                PullRequestId = pullRequestId,
                Title = root.GetProperty("title").GetString()!,
                Description = root.TryGetProperty("description", out var desc) ? desc.GetString()! : "",
                CreatedBy = JsonSerializer.Deserialize<CreatedBy>(
                    root.GetProperty("createdBy").ToString()!,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!,
                CreationDate = root.GetProperty("creationDate").GetDateTime(),
                Status = root.GetProperty("status").GetString()!
            };

            if (root.TryGetProperty("closedDate", out var closedDate))
            {
                prInfo.ClosedDate = closedDate.GetDateTime();
            }

            // Obtener reviewers
            if (root.TryGetProperty("reviewers", out var reviewers) && reviewers.ValueKind == JsonValueKind.Array)
            {
                foreach (var reviewer in reviewers.EnumerateArray())
                {
                    var reviewerInfo = new PasadorScript.DTO.Auditoria.ReviewerInfo
                    {
                        Reviewer = JsonSerializer.Deserialize<CreatedBy>(
                            reviewer.ToString(),
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!,
                        Vote = reviewer.GetProperty("vote").GetInt32() switch
                        {
                            10 => "approved",
                            5 => "approved-with-suggestions",
                            0 => "no-vote",
                            -5 => "waiting-for-author",
                            -10 => "rejected",
                            _ => "unknown"
                        }
                    };

                    if (reviewer.TryGetProperty("votedFor", out var votedFor) && votedFor.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var vote in votedFor.EnumerateArray())
                        {
                            if (vote.TryGetProperty("date", out var voteDate))
                            {
                                reviewerInfo.VotedOn = voteDate.GetDateTime();
                                break;
                            }
                        }
                    }

                    prInfo.Reviewers.Add(reviewerInfo);
                }
            }

            // Obtener commits del PR
            var commitsUrl = $"{baseUrl}/_apis/git/repositories/{root.GetProperty("repository").GetProperty("id").GetString()}/pullRequests/{pullRequestId}/commits?api-version=7.0";
            var commitsResponse = await _httpConfigured.GetAsync(commitsUrl);

            if (commitsResponse.IsSuccessStatusCode)
            {
                var commitsContent = await commitsResponse.Content.ReadAsStringAsync();
                using var commitsDoc = JsonDocument.Parse(commitsContent);
                var commitsRoot = commitsDoc.RootElement;

                if (commitsRoot.TryGetProperty("value", out var commits) && commits.ValueKind == JsonValueKind.Array)
                {
                    foreach (var commit in commits.EnumerateArray())
                    {
                        var commitInfo = new PasadorScript.DTO.Auditoria.CommitInfo
                        {
                            CommitId = commit.GetProperty("commitId").GetString()!,
                            Comment = commit.GetProperty("comment").GetString()!,
                            Author = JsonSerializer.Deserialize<CreatedBy>(
                                commit.GetProperty("author").ToString(),
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!
                        };

                        if (commit.TryGetProperty("author", out var author) &&
                            author.TryGetProperty("date", out var date))
                        {
                            commitInfo.Date = date.GetDateTime();
                        }

                        prInfo.Commits.Add(commitInfo);
                    }
                }
            }

            // ==============================================
            // OBTENER COMENTARIOS DE REVISIONES (THREADS)
            // ==============================================

            // OBTENER THREADS
            try
            {
                var threadsUrl = $"{baseUrl}/_apis/git/repositories/{root.GetProperty("repository").GetProperty("id").GetString()}/pullRequests/{pullRequestId}/threads?api-version=7.0";
                var threadsResponse = await _httpConfigured.GetAsync(threadsUrl);

                if (threadsResponse.IsSuccessStatusCode)
                {
                    var threadsContent = await threadsResponse.Content.ReadAsStringAsync();
                    using var threadsDoc = JsonDocument.Parse(threadsContent);
                    var threadsRoot = threadsDoc.RootElement;

                    if (threadsRoot.TryGetProperty("value", out var threads) && threads.ValueKind == JsonValueKind.Array)
                    {
                        // Procesar threads
                        ProcesarThreads(prInfo, threads);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo threads: {ex.Message}");
            }

            return prInfo;
        }

        public async Task<List<ReleaseDefinition>> GetReleaseDefinitions()
        {
            string baseUrl = Ctx(ProjectType).VsrmUrl;
            string url = $"{baseUrl}/_apis/release/definitions?api-version=6.0";
            var response = await _httpConfigured.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var data = JObject.Parse(json);
            var definitions = new List<ReleaseDefinition>();
            foreach (var item in data["value"])
            {
                definitions.Add(new ReleaseDefinition
                {
                    Id = (int)item["id"],
                    Name = item["name"].ToString()
                });
            }
            return definitions;
        }

        public async Task<List<string>> GetDefinitionEnvironments(string projectName, int definitionId)
        {
            var environments = new List<string>();
            string baseUrl = Ctx(projectName).VsrmUrl;
            string url = $"{baseUrl}/_apis/release/definitions/{definitionId}?api-version=6.0";
            var response = await _httpConfigured.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var data = JObject.Parse(json);
                var envArray = data["environments"] as JArray;
                if (envArray != null)
                {
                    foreach (var env in envArray)
                    {
                        var name = env["name"]?.ToString();
                        if (!string.IsNullOrEmpty(name))
                            environments.Add(name);
                    }
                }
            }
            return environments;
        }

        public async Task<List<ReleaseViewModel>> GetReleasesReport(string projectName, int definitionId, DateTime? desdeUtc, DateTime? hastaUtc, string stageFilter)
        {
            var releases = new List<ReleaseViewModel>();
            string baseUrl = Ctx(projectName).VsrmUrl;
            string query = $"{baseUrl}/_apis/release/releases?definitionId={definitionId}&api-version=6.0&$expand=artifacts,environments";

            //if (desdeUtc.HasValue)
            //    query += $"&minCreatedTime={desdeUtc.Value:yyyy-MM-ddTHH:mm:ssZ}";
            //if (hastaUtc.HasValue)
            //    query += $"&maxCreatedTime={hastaUtc.Value:yyyy-MM-ddTHH:mm:ssZ}";
            query += "&$top=500";

            var response = await _httpConfigured.GetAsync(query);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var data = JObject.Parse(json);
            var items = data["value"];

            foreach (var item in items)
            {
                int releaseId = (int)item["id"];
                string releaseName = item["name"].ToString();
                string releaseDefinition = item["releaseDefinition"]["name"].ToString();
                DateTime createdOn = (DateTime)item["createdOn"];
                string createdBy = item["createdBy"]["displayName"]?.ToString() ?? "";

                // Extraer Build ID y Branch de los artefactos (una sola vez por release)
                string buildId = "N/A";
                string branch = "main";
                var artifacts = item["artifacts"] as JArray;
                if (artifacts != null && artifacts.Count > 0)
                {
                    var firstArtifact = artifacts[0];
                    var defRef = firstArtifact["definitionReference"];

                    // Branch
                    branch = defRef?["branch"]?["name"]?.ToString()
                             ?? defRef?["repository"]?["branch"]?.ToString()
                             ?? "main";

                    // Build ID
                    var buildIdToken = defRef?["buildId"];
                    if (buildIdToken != null)
                    {
                        if (buildIdToken.Type == JTokenType.Object)
                            buildId = buildIdToken["id"]?.ToString() ?? "N/A";
                        else
                            buildId = buildIdToken.ToString();
                    }
                    else if (defRef?["version"] != null)
                    {
                        var versionToken = defRef["version"];
                        if (versionToken.Type == JTokenType.Object)
                            buildId = versionToken["id"]?.ToString() ?? versionToken.ToString();
                        else
                            buildId = versionToken.ToString();
                    }
                    else if (defRef?["id"] != null)
                    {
                        var idToken = defRef["id"];
                        if (idToken.Type == JTokenType.Object)
                            buildId = idToken["id"]?.ToString() ?? idToken.ToString();
                        else
                            buildId = idToken.ToString();
                    }
                }

                var environments = item["environments"] as JArray;
                if (environments != null)
                {
                    foreach (var env in environments)
                    {
                        var stage = env["name"].ToString();

                        // 1. Filtrar por stage si está seleccionado
                        if (!string.IsNullOrEmpty(stageFilter) && stage != stageFilter)
                            continue;

                        DateTime? deployDate = null;

                        var deploySteps = env["deploySteps"] as JArray;
                        var lastDeploy = deploySteps?.LastOrDefault();

                        // 2. Verificar que realmente existe un despliegue en este stage
                        //    (queuedOn no nulo y el stage ha sido ejecutado)
                        DateTime? queuedOn = lastDeploy?["queuedOn"]?.ToObject<DateTime?>();
                        if (!queuedOn.HasValue)
                            continue; // Saltar stages que nunca se desplegaron

                        // 3. Aplicar filtro de fechas al queuedOn (fecha de despliegue)
                        if (desdeUtc.HasValue && queuedOn < desdeUtc.Value)
                            continue;
                        if (hastaUtc.HasValue && queuedOn > hastaUtc.Value)
                            continue;

                        var deployedBy = env["lastModifiedBy"]?["displayName"]?.ToString()
                                      ?? env["createdBy"]?["displayName"]?.ToString()
                                      ?? createdBy;

                        releases.Add(new ReleaseViewModel
                        {
                            ReleaseDefinition = releaseDefinition,
                            Stage = stage,
                            ReleaseId = releaseId,
                            ReleaseName = releaseName,
                            Build = buildId,
                            Branch = branch,
                            DeployedBy = deployedBy,
                            QueuedOn = queuedOn.Value
                        });
                    }
                }
            }

            return releases.OrderByDescending(r => r.QueuedOn).ToList();
        }

        private const int ReleaseListPageSize = 100;
        private const int ReleaseListMaxPages = 20;
        private const int ApprovalHydrationChunkSize = 8;
        private const string ContinuationTokenHeader = "x-ms-continuationtoken";

        /// <inheritdoc />
        public async Task<ReleaseListForReportResult> ListReleasesForReportAsync(
            string projectName,
            int definitionId,
            DateTime minCreatedUtc)
        {
            if (string.IsNullOrWhiteSpace(projectName) || definitionId <= 0)
            {
                return new ReleaseListForReportResult();
            }

            var releases = new List<ReleaseListItemDto>();
            var minCreated = minCreatedUtc.Kind == DateTimeKind.Utc
                ? minCreatedUtc
                : DateTime.SpecifyKind(minCreatedUtc, DateTimeKind.Utc);
            var minCreatedIso = minCreated.ToString("yyyy-MM-ddTHH:mm:ssZ");
            string? continuationToken = null;
            var pageLimitReached = false;

            try
            {
                for (var page = 1; page <= ReleaseListMaxPages; page++)
                {
                    var baseUrl = Ctx(projectName).VsrmUrl;
                    var url =
                        $"{baseUrl}/_apis/release/releases?definitionId={definitionId}" +
                        $"&$expand=artifacts,environments,approvals" +
                        $"&minCreatedTime={Uri.EscapeDataString(minCreatedIso)}" +
                        $"&$top={ReleaseListPageSize}" +
                        $"&queryOrder=descending" +
                        $"&api-version=7.1";

                    if (!string.IsNullOrEmpty(continuationToken))
                        url += $"&continuationToken={Uri.EscapeDataString(continuationToken)}";

                    var response = await _httpConfigured.GetAsync(url);
                    var json = await response.Content.ReadAsStringAsync();
                    var data = JObject.Parse(json);
                    var items = data["value"] as JArray;

                    if (items != null)
                    {
                        foreach (var item in items)
                        {
                            var mapped = MapReleaseListItem(projectName, item);
                            if (mapped != null)
                                releases.Add(mapped);
                        }
                    }

                    continuationToken = ReadContinuationToken(response, data);
                    if (string.IsNullOrEmpty(continuationToken))
                        break;

                    if (page == ReleaseListMaxPages)
                    {
                        pageLimitReached = true;
                        _logger.LogWarning(
                            "Tope de {MaxPages} páginas al listar releases del proyecto {Project} definition {DefinitionId}",
                            ReleaseListMaxPages,
                            projectName,
                            definitionId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error listando releases del proyecto {Project} definition {DefinitionId}",
                    projectName,
                    definitionId);

                return new ReleaseListForReportResult
                {
                    Releases = releases,
                    PageLimitReached = pageLimitReached,
                    Failed = true
                };
            }

            return new ReleaseListForReportResult
            {
                Releases = releases,
                PageLimitReached = pageLimitReached
            };
        }

        /// <inheritdoc />
        public async Task HydrateMissingApprovalsForReportAsync(IReadOnlyCollection<ReleaseListItemDto> releases)
        {
            if (releases is null || releases.Count == 0)
                return;

            var pending = releases
                .Where(NeedsApprovalHydration)
                .DistinctBy(r => (NormalizeProject(r.ProjectName), r.ReleaseId))
                .ToList();

            if (pending.Count == 0)
                return;

            _logger.LogInformation(
                "Hidratando approvals de {Count} releases del informe gerencial (listado sin PRE/POST anidados)",
                pending.Count);

            foreach (var chunk in pending.Chunk(ApprovalHydrationChunkSize))
                await Task.WhenAll(chunk.Select(HydrateReleaseApprovalsAsync));
        }

        private static string? ReadContinuationToken(HttpResponseMessage response, JObject body)
        {
            if (response.Headers.TryGetValues(ContinuationTokenHeader, out var headerValues))
            {
                var headerToken = headerValues.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(headerToken))
                    return headerToken;
            }

            var bodyToken = body["continuationToken"]?.ToString();
            return string.IsNullOrWhiteSpace(bodyToken) ? null : bodyToken;
        }

        private static ReleaseListItemDto? MapReleaseListItem(string projectName, JToken item)
        {
            var idToken = item["id"];
            if (idToken == null || !int.TryParse(idToken.ToString(), out var releaseId) || releaseId <= 0)
                return null;

            var createdOn = item["createdOn"]?.ToObject<DateTime?>() ?? DateTime.MinValue;
            var artifacts = item["artifacts"] as JArray;
            var environments = MapReleaseEnvironments(item["environments"] as JArray);
            ApplyRootApprovals(environments, item["approvals"] as JArray);

            return new ReleaseListItemDto
            {
                ProjectName = projectName,
                ReleaseId = releaseId,
                CreatedOn = createdOn,
                PullRequestId = TryGetPullRequestIdFromArtifacts(artifacts),
                BuildId = TryGetBuildIdFromArtifacts(artifacts),
                Environments = environments
            };
        }

        private static List<ReleaseEnvironmentListItemDto> MapReleaseEnvironments(JArray? environments)
        {
            if (environments == null || environments.Count == 0)
                return [];

            var result = new List<ReleaseEnvironmentListItemDto>(environments.Count);
            foreach (var env in environments)
            {
                var name = env["name"]?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                result.Add(new ReleaseEnvironmentListItemDto
                {
                    Id = env["id"]?.Value<int>() ?? 0,
                    Name = name,
                    Rank = env["rank"]?.Value<int>() ?? 0,
                    QueuedOn = ReadEnvironmentQueuedOn(env),
                    CompletedOn = ReadEnvironmentCompletedOn(env),
                    Status = env["status"]?.ToString() ?? string.Empty,
                    PreDeployApprovals = MapApprovals(env["preDeployApprovals"] as JArray),
                    PostDeployApprovals = MapApprovals(env["postDeployApprovals"] as JArray)
                });
            }

            return result;
        }

        private static void ApplyRootApprovals(
            IReadOnlyList<ReleaseEnvironmentListItemDto> environments,
            JArray? rootApprovals)
        {
            if (rootApprovals == null || rootApprovals.Count == 0 || environments.Count == 0)
                return;

            foreach (var env in environments)
            {
                var forEnv = rootApprovals
                    .Where(a => ApprovalBelongsToEnvironment(a, env))
                    .ToList();
                if (forEnv.Count == 0)
                    continue;

                if (env.PreDeployApprovals.Count == 0)
                {
                    var pre = MapApprovals(forEnv.Where(IsPreDeployApproval));
                    if (pre.Count > 0)
                        env.PreDeployApprovals = pre;
                }

                if (env.PostDeployApprovals.Count == 0)
                {
                    var post = MapApprovals(forEnv.Where(IsPostDeployApproval));
                    if (post.Count > 0)
                        env.PostDeployApprovals = post;
                }
            }
        }

        private static bool ApprovalBelongsToEnvironment(JToken approval, ReleaseEnvironmentListItemDto env)
        {
            var relEnv = approval["releaseEnvironment"];
            var envId = relEnv?["id"]?.Value<int?>();
            if (env.Id > 0 && envId.HasValue && env.Id == envId.Value)
                return true;

            var name = relEnv?["name"]?.ToString()?.Trim();
            return !string.IsNullOrWhiteSpace(name)
                   && string.Equals(name, env.Name, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPreDeployApproval(JToken approval) => IsApprovalType(approval, "preDeploy", 1);

        private static bool IsPostDeployApproval(JToken approval) => IsApprovalType(approval, "postDeploy", 2);

        private static bool IsApprovalType(JToken approval, string name, int numericValue)
        {
            var token = approval["approvalType"];
            if (token == null || token.Type == JTokenType.Null)
                return false;

            var raw = token.Type == JTokenType.Integer
                ? token.Value<int>().ToString()
                : token.ToString();

            if (int.TryParse(raw, out var n))
                return n == numericValue;

            return string.Equals(raw, name, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(raw, name + "Approval", StringComparison.OrdinalIgnoreCase);
        }

        private static IReadOnlyList<ReleaseApprovalListItemDto> MapApprovals(JArray? approvals)
        {
            if (approvals == null || approvals.Count == 0)
                return [];

            return MapApprovals((IEnumerable<JToken>)approvals);
        }

        private static IReadOnlyList<ReleaseApprovalListItemDto> MapApprovals(IEnumerable<JToken> approvals)
        {
            var result = new List<ReleaseApprovalListItemDto>();
            foreach (var approval in approvals)
            {
                var mapped = MapApproval(approval);
                if (mapped != null)
                    result.Add(mapped);
            }

            return result;
        }

        private static ReleaseApprovalListItemDto? MapApproval(JToken approval)
        {
            var status = approval["status"]?.ToString() ?? string.Empty;
            var approver = approval["approver"];
            var approvedBy = approval["approvedBy"];

            return new ReleaseApprovalListItemDto
            {
                Rank = approval["rank"]?.Value<int>() ?? 0,
                Attempt = approval["attempt"]?.Value<int>() ?? 1,
                Status = status,
                IsAutomated = approval["isAutomated"]?.Value<bool>() ?? false,
                ApproverId = approver?["id"]?.ToString(),
                ApproverDisplayName = approver?["displayName"]?.ToString(),
                ApprovedByDisplayName = approvedBy?["displayName"]?.ToString(),
                ModifiedOn = approval["modifiedOn"]?.ToObject<DateTime?>()
            };
        }

        private static bool NeedsApprovalHydration(ReleaseListItemDto? release)
        {
            if (release is null
                || release.ReleaseId <= 0
                || string.IsNullOrWhiteSpace(release.ProjectName)
                || release.Environments.Count == 0)
            {
                return false;
            }

            // Scripts typically already have nested PRE/POST; do not GET those releases.
            return release.Environments.All(e =>
                e.PreDeployApprovals.Count == 0 && e.PostDeployApprovals.Count == 0);
        }

        private async Task HydrateReleaseApprovalsAsync(ReleaseListItemDto release)
        {
            try
            {
                var baseUrl = Ctx(release.ProjectName).VsrmUrl;
                var url = $"{baseUrl}/_apis/release/releases/{release.ReleaseId}?api-version=7.1-preview.8";
                var response = await _httpConfigured.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "No se pudieron hidratar approvals del release {ReleaseId} del proyecto {Project}. Status {StatusCode}",
                        release.ReleaseId,
                        release.ProjectName,
                        (int)response.StatusCode);
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                var data = JObject.Parse(json);
                var fromGet = MapReleaseEnvironments(data["environments"] as JArray);
                ApplyRootApprovals(fromGet, data["approvals"] as JArray);
                MergeApprovalsInto(release.Environments, fromGet);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Error hidratando approvals del release {ReleaseId} del proyecto {Project}",
                    release.ReleaseId,
                    release.ProjectName);
            }
        }

        private static void MergeApprovalsInto(
            IReadOnlyList<ReleaseEnvironmentListItemDto> target,
            IReadOnlyList<ReleaseEnvironmentListItemDto> source)
        {
            if (target.Count == 0 || source.Count == 0)
                return;

            foreach (var env in target)
            {
                var sources = source
                    .Where(s =>
                        (env.Id > 0 && s.Id == env.Id)
                        || string.Equals(s.Name, env.Name, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (sources.Count == 0)
                    continue;

                if (env.PreDeployApprovals.Count == 0)
                {
                    var pre = sources.SelectMany(s => s.PreDeployApprovals).ToList();
                    if (pre.Count > 0)
                        env.PreDeployApprovals = pre;
                }

                if (env.PostDeployApprovals.Count == 0)
                {
                    var post = sources.SelectMany(s => s.PostDeployApprovals).ToList();
                    if (post.Count > 0)
                        env.PostDeployApprovals = post;
                }
            }
        }

        private static string NormalizeProject(string projectName) => projectName.Trim().ToLowerInvariant();

        private static DateTime? ReadEnvironmentQueuedOn(JToken env)
        {
            var lastDeploy = GetLastDeployStep(env);
            var fromStep = lastDeploy?["queuedOn"]?.ToObject<DateTime?>();
            if (fromStep.HasValue)
                return fromStep;

            return env["queuedOn"]?.ToObject<DateTime?>();
        }

        /// <summary>
        /// Lee la hora de fin del ultimo deploy step aplicado.
        /// Prefiere <c>completedOn</c> del step o de sus fases; no usa <c>modifiedOn</c> del environment
        /// (un rechazo actualiza ese campo aunque el deploy no haya quedado aplicado).
        /// </summary>
        /// <param name="env">El token JSON del environment de Azure.</param>
        /// <returns>
        /// La marca UTC de cierre del deploy aplicado, o <see langword="null"/> si no hay step aplicado.
        /// </returns>
        private static DateTime? ReadEnvironmentCompletedOn(JToken env)
        {
            var appliedStep = GetLastAppliedDeployStep(env);
            if (appliedStep == null)
                return null;

            var fromStep = appliedStep["completedOn"]?.ToObject<DateTime?>();
            if (fromStep.HasValue)
                return fromStep;

            var fromPhases = ReadLatestPhaseCompletedOn(appliedStep);
            if (fromPhases.HasValue)
                return fromPhases;

            var fromJob = ReadJobFinishedOn(appliedStep["job"]);
            if (fromJob.HasValue)
                return fromJob;

            var fromTasks = ReadLatestTaskFinishedOn(appliedStep);
            if (fromTasks.HasValue)
                return fromTasks;

            // DeploymentAttempt no tiene completedOn; lastModifiedOn del step aplicado es el cierre del deploy, no el del environment.
            return appliedStep["lastModifiedOn"]?.ToObject<DateTime?>();
        }

        private static JToken? GetLastDeployStep(JToken env)
            => (env["deploySteps"] as JArray)?.LastOrDefault();

        private static JToken? GetLastAppliedDeployStep(JToken env)
        {
            if (env["deploySteps"] is not JArray steps || steps.Count == 0)
                return null;

            for (var i = steps.Count - 1; i >= 0; i--)
            {
                var status = steps[i]["status"]?.ToString();
                if (EnvironmentDto.EsEstadoAplicado(status))
                    return steps[i];
            }

            return null;
        }

        private static DateTime? ReadLatestPhaseCompletedOn(JToken step)
        {
            if (step["releaseDeployPhases"] is not JArray phases || phases.Count == 0)
                return null;

            DateTime? latest = null;
            foreach (var phase in phases)
            {
                var completed = phase["completedOn"]?.ToObject<DateTime?>();
                if (completed.HasValue && (!latest.HasValue || completed.Value > latest.Value))
                    latest = completed;
            }

            return latest;
        }

        private static DateTime? ReadJobFinishedOn(JToken? job)
        {
            if (job == null || job.Type == JTokenType.Null)
                return null;

            return job["finishTime"]?.ToObject<DateTime?>()
                   ?? job["dateEnded"]?.ToObject<DateTime?>();
        }

        private static DateTime? ReadLatestTaskFinishedOn(JToken step)
        {
            if (step["releaseDeployPhases"] is not JArray phases || phases.Count == 0)
                return null;

            DateTime? latest = null;
            foreach (var phase in phases)
            {
                if (phase["deploymentJobs"] is not JArray jobs)
                    continue;

                foreach (var job in jobs)
                {
                    var jobFinished = ReadJobFinishedOn(job["job"]);
                    if (jobFinished.HasValue && (!latest.HasValue || jobFinished.Value > latest.Value))
                        latest = jobFinished;

                    if (job["tasks"] is not JArray tasks)
                        continue;

                    foreach (var task in tasks)
                    {
                        var finished = task["finishTime"]?.ToObject<DateTime?>()
                                       ?? task["dateEnded"]?.ToObject<DateTime?>();
                        if (finished.HasValue && (!latest.HasValue || finished.Value > latest.Value))
                            latest = finished;
                    }
                }
            }

            return latest;
        }

        private static int? TryGetPullRequestIdFromArtifacts(JArray? artifacts)
        {
            if (artifacts == null)
                return null;

            foreach (var artifact in artifacts)
            {
                var defRef = artifact["definitionReference"];
                if (defRef == null)
                    continue;

                var fromField = TryParseIdToken(defRef["pullRequestId"])
                    ?? TryParseIdToken(defRef["pullRequest"]);
                if (fromField.HasValue)
                    return fromField;

                var branch = defRef["branch"]?["name"]?.ToString()
                    ?? defRef["branch"]?["id"]?.ToString();
                var fromBranch = PullRequestIdExtractor.TryFromBranch(branch);
                if (fromBranch.HasValue)
                    return fromBranch;
            }

            return null;
        }

        private static int? TryGetBuildIdFromArtifacts(JArray? artifacts)
        {
            if (artifacts == null)
                return null;

            foreach (var artifact in artifacts)
            {
                var defRef = artifact["definitionReference"];
                if (defRef == null)
                    continue;

                var buildId = TryParseIdToken(defRef["buildId"])
                    ?? TryParseIdToken(defRef["version"]);
                if (buildId.HasValue)
                    return buildId;
            }

            return null;
        }

        private static int? TryParseIdToken(JToken? token)
        {
            if (token == null || token.Type == JTokenType.Null)
                return null;

            var raw = token.Type == JTokenType.Object
                ? token["id"]?.ToString() ?? token["name"]?.ToString()
                : token.ToString();

            return int.TryParse(raw, out var id) && id > 0 ? id : null;
        }

        // Método para crear CommentInfo
        private CommentInfo CrearCommentInfo(JsonElement comment, JsonElement thread)
        {
            var commentInfo = new CommentInfo
            {
                Id = comment.GetProperty("id").GetInt32(),
                Content = comment.GetProperty("content").GetString()!,
                CommentType = comment.GetProperty("commentType").GetString()!,
                PublishedDate = comment.GetProperty("publishedDate").GetDateTime(),
                LastUpdatedDate = comment.GetProperty("lastUpdatedDate").GetDateTime(),
                ThreadContext = GetThreadContext(thread)
            };

            // Obtener autor
            if (comment.TryGetProperty("author", out var author))
            {
                commentInfo.Author = JsonSerializer.Deserialize<CreatedBy>(
                    author.ToString(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            return commentInfo;
        }

        // Método auxiliar para obtener contexto del hilo
        private static string GetThreadContext(JsonElement thread)
        {
            // Verificar threadContext primero
            if (thread.TryGetProperty("threadContext", out var threadContext) &&
                threadContext.ValueKind != JsonValueKind.Null &&
                threadContext.TryGetProperty("filePath", out var filePath))
            {
                return filePath.GetString() ?? "general";
            }

            // Luego pullRequestThreadContext
            if (thread.TryGetProperty("pullRequestThreadContext", out var prThreadContext) &&
                prThreadContext.ValueKind != JsonValueKind.Null &&
                prThreadContext.TryGetProperty("filePath", out var prFilePath))
            {
                return prFilePath.GetString() ?? "general";
            }

            return "general";
        }
        // Método para procesar threads
        private void ProcesarThreads(PullRequestAuditInfo prInfo, JsonElement threads)
        {
            // Primero, procesar todos los threads para recolectar información
            var threadsInfo = new List<ThreadInfo>();
            var votosPorIdentidad = new Dictionary<string, VotoInfo>();

            foreach (var thread in threads.EnumerateArray())
            {
                try
                {
                    // Obtener información básica del thread
                    var threadId = thread.GetProperty("id").GetInt32();
                    var isDeleted = thread.TryGetProperty("isDeleted", out var deletedProp) && deletedProp.GetBoolean();

                    if (isDeleted) continue;

                    // Obtener propiedades del thread
                    var properties = ObtenerProperties(thread);

                    // Procesar comentarios
                    if (thread.TryGetProperty("comments", out var commentsArray) &&
                        commentsArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var comment in commentsArray.EnumerateArray())
                        {
                            var commentType = comment.GetProperty("commentType").GetString();

                            // Procesar comentarios de sistema (votos) para asociar identidades
                            if (commentType == "system" && properties != null)
                            {
                                ProcesarComentarioSistema(comment, properties, votosPorIdentidad);
                            }

                            // Procesar comentarios de texto
                            if (commentType == "text")
                            {
                                var commentInfo = CrearCommentInfo(comment, thread);
                                var threadInfo = new ThreadInfo
                                {
                                    Id = threadId,
                                    Status = thread.TryGetProperty("status", out var status) ? status.GetString() : "active",
                                    ThreadContext = GetThreadContext(thread),
                                    Properties = properties
                                };

                                threadInfo.Comments.Add(commentInfo);
                                threadsInfo.Add(threadInfo);

                                Console.WriteLine($"Encontrado comentario de texto: {commentInfo.Content}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error procesando thread: {ex.Message}");
                }
            }

            // Asociar comentarios con revisores
            AsociarComentariosConRevisores(prInfo, threadsInfo, votosPorIdentidad);
        }

        // Método para obtener propiedades del thread
        private Dictionary<string, object> ObtenerProperties(JsonElement thread)
        {
            if (thread.TryGetProperty("properties", out var properties) &&
                properties.ValueKind != JsonValueKind.Null)
            {
                try
                {
                    return JsonSerializer.Deserialize<Dictionary<string, object>>(properties.ToString());
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

        // Método para procesar comentarios del sistema (votos)
        private void ProcesarComentarioSistema(JsonElement comment, Dictionary<string, object> properties,
                                              Dictionary<string, VotoInfo> votosPorIdentidad)
        {
            // Verificar si es un comentario de voto
            if (properties.TryGetValue("CodeReviewThreadType", out var threadTypeObj))
            {
                var threadType = threadTypeObj.ToString();

                if (threadType.Contains("VoteUpdate") || threadType.Contains("Vote"))
                {
                    // Obtener identidad del votante
                    if (properties.TryGetValue("CodeReviewVotedByIdentity", out var voterIdObj))
                    {
                        var voterId = ExtraerValorDePropiedad(voterIdObj);

                        // Obtener resultado del voto
                        if (properties.TryGetValue("CodeReviewVoteResult", out var voteResultObj))
                        {
                            var voteResult = ExtraerValorDePropiedad(voteResultObj);

                            // Almacenar información del voto
                            votosPorIdentidad[voterId] = new VotoInfo
                            {
                                VoterIdentity = voterId,
                                VoteResult = voteResult,
                                Comment = comment.GetProperty("content").GetString()
                            };

                            Console.WriteLine($"Voto encontrado: {voterId} -> {voteResult}");
                        }
                    }
                }
            }
        }

        // Método para extraer valores de propiedades
        private string ExtraerValorDePropiedad(object propertyObj)
        {
            if (propertyObj == null) return null;

            try
            {
                var jsonElement = JsonDocument.Parse(propertyObj.ToString()).RootElement;

                if (jsonElement.TryGetProperty("$value", out var valueProp))
                {
                    return valueProp.GetString();
                }
                else
                {
                    return propertyObj.ToString();
                }
            }
            catch
            {
                return propertyObj.ToString();
            }
        }

        // Método para asociar comentarios con revisores
        private void AsociarComentariosConRevisores(PullRequestAuditInfo prInfo, List<ThreadInfo> threadsInfo,
                                                  Dictionary<string, VotoInfo> votosPorIdentidad)
        {
            // Crear diccionario de revisores por diferentes identificadores
            var revisoresPorId = prInfo.Reviewers.ToDictionary(
                r => r.Reviewer.Id.ToString().ToLower(),
                r => r,
                StringComparer.OrdinalIgnoreCase
            );

            var revisoresPorUniqueName = prInfo.Reviewers.ToDictionary(
                r => r.Reviewer.UniqueName?.ToLower() ?? "",
                r => r,
                StringComparer.OrdinalIgnoreCase
            );

            var revisoresPorDisplayName = prInfo.Reviewers.ToDictionary(
                r => r.Reviewer.DisplayName?.ToLower() ?? "",
                r => r,
                StringComparer.OrdinalIgnoreCase
            );

            // Para cada thread con comentarios de texto
            foreach (var thread in threadsInfo)
            {
                foreach (var comment in thread.Comments)
                {
                    bool asociado = false;

                    // Intentar asociar por autor del comentario
                    if (comment.Author != null)
                    {
                        // Buscar por Id
                        if (revisoresPorId.TryGetValue(comment.Author.Id.ToString().ToLower(), out var reviewer))
                        {
                            reviewer.Comments.Add(comment);
                            asociado = true;
                            Console.WriteLine($"Comentario asociado a {reviewer.Reviewer.DisplayName} por Id");
                        }
                        // Buscar por UniqueName
                        else if (!string.IsNullOrEmpty(comment.Author.UniqueName) &&
                                 revisoresPorUniqueName.TryGetValue(comment.Author.UniqueName.ToLower(), out reviewer))
                        {
                            reviewer.Comments.Add(comment);
                            asociado = true;
                            Console.WriteLine($"Comentario asociado a {reviewer.Reviewer.DisplayName} por UniqueName");
                        }
                        // Buscar por DisplayName
                        else if (!string.IsNullOrEmpty(comment.Author.DisplayName) &&
                                 revisoresPorDisplayName.TryGetValue(comment.Author.DisplayName.ToLower(), out reviewer))
                        {
                            reviewer.Comments.Add(comment);
                            asociado = true;
                            Console.WriteLine($"Comentario asociado a {reviewer.Reviewer.DisplayName} por DisplayName");
                        }
                    }

                    // Si no se asoció, intentar por proximidad temporal con votos
                    if (!asociado && thread.Properties != null)
                    {
                        // Buscar el voto más cercano en tiempo a este comentario
                        var comentarioFecha = comment.PublishedDate;
                        var votoMasCercano = votosPorIdentidad.Values
                            .OrderBy(v => Math.Abs((comentarioFecha - DateTime.Parse(v.CommentDate)).TotalSeconds))
                            .FirstOrDefault();

                        if (votoMasCercano != null)
                        {
                            // Buscar revisor por la identidad del votante
                            // La identidad del votante puede estar en diferentes formatos
                            var voterId = votoMasCercano.VoterIdentity;

                            // Buscar en todos los diccionarios
                            var reviewer = prInfo.Reviewers.FirstOrDefault(r =>
                                r.Reviewer.Id.ToString().Contains(voterId, StringComparison.OrdinalIgnoreCase) ||
                                r.Reviewer.UniqueName?.Contains(voterId, StringComparison.OrdinalIgnoreCase) == true ||
                                r.Reviewer.DisplayName?.Contains(voterId, StringComparison.OrdinalIgnoreCase) == true
                            );

                            if (reviewer != null)
                            {
                                reviewer.Comments.Add(comment);
                                asociado = true;
                                Console.WriteLine($"Comentario asociado a {reviewer.Reviewer.DisplayName} por proximidad con voto");
                            }
                        }
                    }

                    if (!asociado)
                    {
                        Console.WriteLine($"Comentario NO asociado: {comment.Content}");
                        // Agregar a una lista general de comentarios no asociados
                        prInfo.ComentariosNoAsociados.Add(comment);
                    }
                }
            }
        }

        // Método auxiliar para asociar comentarios con reviewers
        
        private DeploymentInfo ExtractDeploymentInfo(JsonElement env)
        {
            var deployInfo = new DeploymentInfo();

            try
            {
                // RequestedBy (quien disparó el deploy)
                if (env.TryGetProperty("owner", out var owner) && owner.ValueKind == JsonValueKind.Object)
                {
                    deployInfo.RequestedBy = JsonSerializer.Deserialize<CreatedBy>(owner.ToString(), new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    })!;
                }

                // Buscar el último deployStep para obtener quien desplegó y timestamps
                if (env.TryGetProperty("deploySteps", out var deploySteps) && deploySteps.ValueKind == JsonValueKind.Array)
                {
                    var lastStep = deploySteps.EnumerateArray().LastOrDefault();
                    if (lastStep.ValueKind != JsonValueKind.Undefined)
                    {
                        // DeployedBy (requestedBy dentro del último step)
                        if (lastStep.TryGetProperty("requestedBy", out var requestedBy) && requestedBy.ValueKind == JsonValueKind.Object)
                        {
                            deployInfo.DeployedBy = JsonSerializer.Deserialize<CreatedBy>(requestedBy.ToString(), new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            })!;
                        }

                        // StartedOn - queuedOn del último step si existe
                        if (lastStep.TryGetProperty("queuedOn", out var queuedOn) && queuedOn.TryGetDateTime(out var qd))
                            deployInfo.StartedOn = qd;

                        // CompletedOn - lastModifiedOn del último step si existe
                        if (lastStep.TryGetProperty("lastModifiedOn", out var lastModified) && lastModified.TryGetDateTime(out var ld))
                            deployInfo.CompletedOn = ld;
                    }
                }

                // Trigger reason / reason
                if (env.TryGetProperty("triggerReason", out var reason) && reason.ValueKind == JsonValueKind.String)
                {
                    deployInfo.Reason = reason.GetString()!;
                }
                else if (env.TryGetProperty("reason", out var reason2) && reason2.ValueKind == JsonValueKind.String)
                {
                    deployInfo.Reason = reason2.GetString()!;
                }

                // Calcular duración si ambos timestamps presentes
                if (deployInfo.StartedOn.HasValue && deployInfo.CompletedOn.HasValue)
                {
                    deployInfo.Duration = deployInfo.CompletedOn.Value - deployInfo.StartedOn.Value;
                }
            }
            catch
            {
                // No bloquear flujo por fallos al parsear campo de deployment; se devuelve objeto parcial.
            }

            return deployInfo;
        }

    }
}
