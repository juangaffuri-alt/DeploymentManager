using PasadorScript.DTO.Auditoria;
using PasadorScript.DTO.AzureDev;
using PasadorScript.Services.Graph;
using PasadorScript.Services.User;
using System.Net.Http.Headers;
using System.Security.Policy;
using System.Text;
using System.Text.Json;

namespace PasadorScript.Services.Azure
{
    public class AzurePullRequestService : AzureBaseService, IAzurePullRequestService
    {
        private readonly ILogger<AzurePullRequestService> _logger;
        private readonly IEventPublisher _publisher;
        private readonly IUserContext _userContext;
        private readonly IServiciosGraph _serviciosGraph;
        private readonly AzureHttpClientConfigured _httpConfigured;
        private readonly AzureHttpClientUser _httpUser;
        private readonly AzureHttpClientLegacy _httpUserLegacy;
        private readonly IAzureCommitService _azureCommitService;

        public AzurePullRequestService(IConfiguration configuration, ILogger<AzurePullRequestService> logger, IEventPublisher eventPublisher,
            IUserContext userContext, IServiciosGraph serviciosGraph, AzureHttpClientConfigured azureHttpClientConfigured, 
            AzureHttpClientUser httpUser, AzureHttpClientLegacy azureHttpClientLegacy, IAzureCommitService azureCommitService) : base(configuration)
        {
            _logger = logger;
            _publisher = eventPublisher;
            _userContext = userContext;
            _serviciosGraph = serviciosGraph;
            _httpConfigured = azureHttpClientConfigured;
            _httpUser = httpUser;
            _httpUserLegacy = azureHttpClientLegacy;
            _azureCommitService = azureCommitService;
        }
        public async Task<PullRequestInfo?> ObtenerInfoPullRequestAsync(int prId)
        {
            string baseUrl = Ctx(ProjectType).BaseUrl;
            var url = $"{baseUrl}/_apis/git/pullrequests/{prId}?api-version=7.1-preview.1";

            var response = await _httpConfigured.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);

            var pr = doc.RootElement;

            return new PullRequestInfo
            {
                Id = pr.GetProperty("pullRequestId").GetInt32(),
                Status = pr.GetProperty("status").GetString() ?? "",
                TargetBranch = pr.GetProperty("targetRefName").GetString()?.Replace("refs/heads/", "") ?? "",
                SourceBranch = pr.GetProperty("sourceRefName").GetString()?.Replace("refs/heads/", "") ?? "",
                LastMergeCommit = pr.TryGetProperty("lastMergeCommit", out var commit) ?
                    new DTO.AzureDev.CommitInfo { CommitId = commit.GetProperty("commitId").GetString() ?? "" } : null
            };
        }
        public async Task AbandonarPullRequestAsync(string repositoryId, int pullRequestId)
        {
            var baseUrl = Ctx(ProjectType).BaseUrl;
            // Endpoint
            var url = $"{baseUrl}/_apis/git/repositories/{repositoryId}/pullRequests/{pullRequestId}?api-version=7.0";

            // Body para cambiar estado a "abandoned"
            var body = new { status = "abandoned" };            

            var response = await _httpUser.SendAsync(HttpMethod.Patch, url, body);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug($"Pull request #{pullRequestId} abandonado correctamente.");
            }
            else
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Error al abandonar el PR: {response.StatusCode} - {responseContent}");
            }
        }

        public async Task AddReviewerToPR(string repositoryId, int prId, params string[] reviewerDescriptors)
        {
            foreach (var reviewerDescriptor in reviewerDescriptors)
            {
                //
                var baseUrl = $"{Ctx(ProjectType).BaseUrl}/_apis/git/repositories/{repositoryId}/pullRequests/{prId}/reviewers/{reviewerDescriptor}?api-version=7.1-preview.1";

                var payload = new
                {
                    id = reviewerDescriptor,
                    isRequired = true,
                    vote = 0,
                };

                var response = await _httpUser.SendAsync(HttpMethod.Put, baseUrl, payload);
                var result = await response.Content.ReadAsStringAsync();

                _logger.LogDebug($"Status: {response.StatusCode}");
                _logger.LogDebug(result);
            }
        }

        public async Task<bool> CompletePullRequestAsync(string repositoryId, int pullRequestId)
        {
            // 1. Obtener detalles del PR para extraer el commitId
            var prUrl = $"{Ctx(ProjectType).BaseUrl}/_apis/git/repositories/{repositoryId}/pullRequests/{pullRequestId}?api-version=7.1-preview.1";

            var prResponse = await _httpConfigured.GetAsync(prUrl);
            prResponse.EnsureSuccessStatusCode();

            using var prStream = await prResponse.Content.ReadAsStreamAsync();
            var prDetails = await JsonSerializer.DeserializeAsync<AzureDevopsPullRequest>(prStream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var lastSourceCommitId = prDetails?.LastMergeSourceCommit?.CommitId;
            if (string.IsNullOrEmpty(lastSourceCommitId))
            {
                _logger.LogError($"No se pudo obtener el último commit de origen del PR. Repository {repositoryId}. PullRequestID {pullRequestId}");
                return false;
            }

            // 2. Preparar el PATCH con lastMergeSourceCommit
            var completeUrl = prUrl; // misma URL

            var payload = new
            {
                status = "completed",
                lastMergeSourceCommit = new { commitId = lastSourceCommitId },
                completionOptions = new
                {
                    deleteSourceBranch = true
                }
            };

            var response = await _httpUser.SendAsync(HttpMethod.Patch, completeUrl, payload);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Error al completar PR #{pullRequestId}: {error}");
                return false;
            }
        }

        public async Task<int> CreateBranchAndPullRequestAsync(string sourceBranch, string targetBranch, string repositoryId, int prIdOrig)
        {
            string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            string newBranchName = $"promote-{sourceBranch}-{timestamp}";
            string newRefName = $"refs/heads/{newBranchName}";

            // 1. Obtener el último commit de la rama source (qa)
            var branchInfoUrl = $"{Ctx(ProjectType).BaseUrl}/_apis/git/repositories/{repositoryId}/refs?filter=heads/{sourceBranch}&api-version=7.0";
            var branchResponse = await _httpConfigured.GetAsync(branchInfoUrl);
            branchResponse.EnsureSuccessStatusCode();

            var branchContent = await branchResponse.Content.ReadAsStringAsync();
            var branchJson = JsonDocument.Parse(branchContent);
            var commitId = branchJson.RootElement.GetProperty("value")[0].GetProperty("objectId").GetString();

            // 2. Crear nueva rama temporal
            var createRefPayload = new
            {
                name = newRefName,
                oldObjectId = "0000000000000000000000000000000000000000",
                newObjectId = commitId
            };
            var createRefUrl = $"{Ctx(ProjectType).BaseUrl}/_apis/git/repositories/{repositoryId}/refs?api-version=7.1-preview.1";

            var createRefResponse = await _httpUser.SendAsync(HttpMethod.Post, createRefUrl, createRefPayload);
            createRefResponse.EnsureSuccessStatusCode();

            // 3. Crear Pull Request
            var createPrPayload = new
            {
                sourceRefName = newRefName,
                targetRefName = $"refs/heads/{targetBranch}",
                title = $"Promote scripts desde {sourceBranch} a {targetBranch}",
                description = $"Auto-gen PR to promote scripts desde `{sourceBranch}` a `{targetBranch}`.",
            };

            string prUrl = $"{Ctx(ProjectType).BaseUrl}/_apis/git/repositories/{repositoryId}/pullrequests?api-version=7.1-preview.1";
            
            var prResponse = await _httpUser.SendAsync(HttpMethod.Post, prUrl, createPrPayload);
            prResponse.EnsureSuccessStatusCode();
            if (prResponse.IsSuccessStatusCode)
            {
                using var stream = await prResponse.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(stream);
                int prId = doc.RootElement.GetProperty("pullRequestId").GetInt32();
                //observers
                var evento = new PullRequestUpdated()
                {
                    PullRequestId = prIdOrig,
                    NewPullRequestId = prId,
                    BranchName = newBranchName
                };
                await _publisher.PublishAsync(evento);
                return prId;
            }
            else
            {
                string error = await prResponse.Content.ReadAsStringAsync();
                _logger.LogError($"Error al crear el PR: {prResponse.StatusCode}. Error {error}");
                
            }
            return 0;
        }

        public async Task EnableAutoCompleteForPR(string repositoryId, int prId, bool deleteSourceBranch = true, string mergeStrategy = "merge")
        {
            var patUsr = _userContext.PersonalAccessToken!;
            var myIds = await _serviciosGraph.GetMyIdAsAsync(patUsr);
            var prInfo = await GetPullRequestAsync(prId);
            var idAutoComplete = myIds.Id;
            if (prInfo != null && prInfo.CreatedBy != null && !String.IsNullOrEmpty(prInfo.CreatedBy.Id))
                idAutoComplete = prInfo.CreatedBy.Id;

            var baseUrl = $"{Ctx(ProjectType).BaseUrl}/_apis/git/repositories/{repositoryId}/pullRequests/{prId}?api-version=7.1-preview.1";

            var payload = new
            {
                autoCompleteSetBy = new
                {
                    id = idAutoComplete
                },
                completionOptions = new
                {
                    deleteSourceBranch = deleteSourceBranch,
                    mergeStrategy = mergeStrategy,
                    mergeCommitMessage = $"Auto-completed PR #{prId}"
                }
            };

            var response = await _httpUser.SendAsync(HttpMethod.Patch, baseUrl, payload); // PATCH para actualizar el PR

            var result = await response.Content.ReadAsStringAsync();
            _logger.LogDebug($"AutoComplete Status: {response.StatusCode}. Result: {result}");
        }
        public async Task<AzureDevopsPullRequest?> GetPullRequestAsync(int pullRequestId)
        {
            var _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var baseUrl = Ctx(ProjectType).BaseUrl;
            var url = $"{baseUrl}/_apis/git/pullrequests/{pullRequestId}?api-version=6.0";

            var response = await _httpConfigured.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<AzureDevopsPullRequest>(content, _jsonOptions);
        }

        public async Task<AzureDevopsPullRequest?> GetPullRequestAsync(string projectName, int pullRequestId)
        {
            var _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var baseUrl = Ctx(projectName).BaseUrl;
            var url = $"{baseUrl}/_apis/git/pullrequests/{pullRequestId}?api-version=6.0";

            var response = await _httpConfigured.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<AzureDevopsPullRequest>(content, _jsonOptions);
        }

        public string GenerateFileDiffView(FileDetailedDiff fileDiff)
        {
            if (!string.IsNullOrEmpty(fileDiff.Error))
                return $"Error: {fileDiff.Error}";

            if (fileDiff.DiffLines.Count == 0)
                return "No changes found";

            var sb = new StringBuilder();

            // Header del archivo
            sb.AppendLine($"# {Path.GetFileName(fileDiff.FilePath)}");
            sb.AppendLine($"## {fileDiff.FilePath}");
            sb.AppendLine();
            sb.AppendLine($"Changes: +{fileDiff.AddedLines} -{fileDiff.DeletedLines}");
            sb.AppendLine(new string('-', 80));
            sb.AppendLine();

            // Líneas de diff
            foreach (var line in fileDiff.DiffLines)
            {
                var lineInfo = line.LineType switch
                {
                    LineType.Added => $"    +{line.NewLineNumber}",
                    LineType.Deleted => $"{line.OldLineNumber}    ",
                    _ => $"{line.OldLineNumber}    +{line.NewLineNumber}"
                };

                sb.AppendLine($"{lineInfo} {line.LinePrefix} {line.Content}");
            }

            return sb.ToString();
        }

        public string GeneratePortalLikeView(PRCompleteDataWithDiffs prData)
        {
            var sb = new StringBuilder();

            // Header del PR
            sb.AppendLine($"# PR de {prData.PRDetails.LastMergeSourceCommit.CommitId.Substring(0, 8)} a main");
            sb.AppendLine();
            sb.AppendLine($"{prData.PRDetails.Status}  ");
            sb.AppendLine($"{prData.PRDetails.PullRequestId}  ");
            sb.AppendLine($"{prData.PRDetails.Repository.Name} proposes to merge {prData.PRDetails.LastMergeSourceCommit.CommitId.Substring(0, 8)} into main  ");
            sb.AppendLine();
            sb.AppendLine(new string('-', 60));

            // Lista de archivos
            sb.AppendLine("## All Changes");
            sb.AppendLine($"**{prData.Summary.TotalFilesChanged} changed files**  ");
            sb.AppendLine();

            foreach (var file in prData.Files)
            {
                var changeSymbol = file.ChangeType switch
                {
                    "add" => "[+]",
                    "delete" => "[-]",
                    _ => "[~]"
                };

                var stats = prData.FileDiffs.ContainsKey(file.Path)
                    ? $" (+{prData.FileDiffs[file.Path].AddedLines}/-{prData.FileDiffs[file.Path].DeletedLines})"
                    : "";

                sb.AppendLine($"{changeSymbol} {file.Path}{stats}");
            }

            sb.AppendLine();
            sb.AppendLine(new string('=', 80));

            // Diffs detallados por archivo
            foreach (var file in prData.Files.Where(f => prData.FileDiffs.ContainsKey(f.Path)))
            {
                var fileDiff = prData.FileDiffs[file.Path];
                if (fileDiff.DiffLines.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine($"## {Path.GetFileName(file.Path)}");
                    sb.AppendLine($"### {file.Path}");
                    sb.AppendLine();

                    foreach (var line in fileDiff.DiffLines.Take(50)) // Mostrar primeras 50 líneas
                    {
                        sb.AppendLine($"{line.LinePrefix} {line.Content}");
                    }

                    if (fileDiff.DiffLines.Count > 50)
                    {
                        sb.AppendLine($"... and {fileDiff.DiffLines.Count - 50} more lines");
                    }

                    sb.AppendLine(new string('-', 40));
                }
            }

            return sb.ToString();
        }

        /// <inheritdoc />
        public bool LastLabelsFailed { get; private set; }

        public async Task<int> GeneratePullRequest(
            string repositoryId,
            string title,
            string description,
            string sourceBranch,
            string targetBranch,
            bool enableAutoComplete,
            IReadOnlyList<string>? labels = null,
            params object[] reviewers)
        {
            LastLabelsFailed = false;

            var resolvedTitle = title?.Trim() ?? string.Empty;
            if (resolvedTitle.Length == 0)
            {
                resolvedTitle = $"PR de {sourceBranch} a {targetBranch}";
            }

            var baseData = new Dictionary<string, object>
            {
                ["sourceRefName"] = $"refs/heads/{sourceBranch}",
                ["targetRefName"] = $"refs/heads/{targetBranch}",
                ["title"] = resolvedTitle,
                ["description"] = description?.Trim() ?? string.Empty,
                ["completionOptions"] = new
                {
                    deleteSourceBranch = true,
                    mergeStrategy = "merge",
                    mergeCommitMessage = $"Merged PR {sourceBranch} to {targetBranch}"
                }
            };

            string prUrl = $"{Ctx(ProjectType).BaseUrl}/_apis/git/repositories/{repositoryId}/pullrequests?api-version=7.1-preview.1";

            HttpResponseMessage response = await _httpUser.SendAsync(HttpMethod.Post, prUrl, baseData);

            if (response.IsSuccessStatusCode)
            {
                using var stream = await response.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(stream);
                int prId = doc.RootElement.GetProperty("pullRequestId").GetInt32();
                if (prId > 0)
                {
                    await AplicarLabelsAsync(repositoryId, prId, labels);
                }
                return prId;
            }
            else
            {
                string error = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Error al crear el PR: {response.StatusCode}. Error {error}");
            }
            return 0;
        }

        private async Task AplicarLabelsAsync(string repositoryId, int pullRequestId, IReadOnlyList<string>? labels)
        {
            if (labels is null || labels.Count == 0)
            {
                return;
            }

            var names = labels
                .Select(l => l?.Trim())
                .Where(l => !string.IsNullOrEmpty(l))
                .Select(l => l!)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            foreach (var label in names)
            {
                var labelUrl = $"{Ctx(ProjectType).BaseUrl}/_apis/git/repositories/{repositoryId}/pullRequests/{pullRequestId}/labels?api-version=7.1";
                try
                {
                    var response = await _httpUser.SendAsync(HttpMethod.Post, labelUrl, new { name = label });
                    if (!response.IsSuccessStatusCode)
                    {
                        LastLabelsFailed = true;
                        _logger.LogWarning(
                            "No se pudo aplicar la label {Label} al PR {PullRequestId} del repositorio {RepositoryId}. Status {StatusCode}",
                            label, pullRequestId, repositoryId, (int)response.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    LastLabelsFailed = true;
                    _logger.LogWarning(
                        ex,
                        "No se pudo aplicar la label {Label} al PR {PullRequestId} del repositorio {RepositoryId}",
                        label, pullRequestId, repositoryId);
                }
            }
        }

        public async Task<PRCompleteData> GetPRCompleteDataAsync(int pullRequestId)
        {
            var result = new PRCompleteData();

            try
            {
                // 1. Obtener información básica del PR
                result.PRDetails = await GetPRDetailsAsync(pullRequestId);

                // 2. Obtener archivos modificados
                result.Files = await GetPRFilesAlternativeAsync(pullRequestId);

                // 3. Obtener estadísticas para cada archivo y calcular resumen
                result.Summary = new PRSummary();

                foreach (var file in result.Files)
                {
                    file.ChangeStats = await GetFileChangeStatsAsync(
                        result.PRDetails.Repository.Id,
                        result.PRDetails.LastMergeSourceCommit.CommitId,
                        result.PRDetails.LastMergeTargetCommit.CommitId,
                        file.Path
                    );

                    // Actualizar resumen
                    result.Summary.TotalLinesAdded += file.ChangeStats.Added;
                    result.Summary.TotalLinesDeleted += file.ChangeStats.Deleted;

                    switch (file.ChangeType)
                    {
                        case "add":
                            result.Summary.AddedFiles++;
                            break;
                        case "edit":
                            result.Summary.ModifiedFiles++;
                            break;
                        case "delete":
                            result.Summary.DeletedFiles++;
                            break;
                    }
                }

                result.Summary.TotalFilesChanged = result.Files.Count;
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        public async Task<PRCompleteDataWithDiffs> GetPRCompleteDataWithDiffsAsync(int pullRequestId)
        {
            var result = new PRCompleteDataWithDiffs();

            try
            {
                // Obtener data básica del PR
                var basicData = await GetPRCompleteDataAsync(pullRequestId);
                result.PRDetails = basicData.PRDetails;
                result.Files = basicData.Files;
                result.Summary = basicData.Summary;
                result.Success = basicData.Success;
                result.ErrorMessage = basicData.ErrorMessage;

                if (!basicData.Success)
                    return result;

                // Obtener diffs detallados para cada archivo
                result.FileDiffs = new Dictionary<string, FileDetailedDiff>();

                foreach (var file in basicData.Files)
                {
                    var detailedDiff = await GetFileDetailedDiffAsync(
                        basicData.PRDetails.Repository.Id,
                        basicData.PRDetails.LastMergeSourceCommit.CommitId,
                        basicData.PRDetails.LastMergeTargetCommit.CommitId,
                        file.Path
                    );

                    result.FileDiffs[file.Path] = detailedDiff;
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }



        public async Task<PullRequestHistory> GetPullRequestHistoryAsync(string repositoryId, int prId)
        {
            string baseUrl = $"{Ctx(ProjectType).BaseUrl}/_apis/git/repositories/{repositoryId}/pullRequests/{prId}?api-version=7.1-preview.1";
            string reviewersUrl = $"{Ctx(ProjectType).BaseUrl}/_apis/git/repositories/{repositoryId}/pullRequests/{prId}/reviewers?api-version=7.1-preview.1";

            // 1. PR principal
            var prResponse = await _httpConfigured.GetAsync(baseUrl);
            prResponse.EnsureSuccessStatusCode();
            var prJson = JsonDocument.Parse(await prResponse.Content.ReadAsStringAsync()).RootElement;

            var prHistory = new PullRequestHistory
            {
                Id = prJson.GetProperty("pullRequestId").GetInt32(),
                Title = prJson.GetProperty("title").GetString()!,
                Status = prJson.GetProperty("status").GetString()!,
                Author = prJson.GetProperty("createdBy").GetProperty("displayName").GetString()!,
                CreatedDate = prJson.GetProperty("creationDate").GetDateTime(),
            };

            // 2. Reviewers actuales y su último voto
            var reviewersResponse = await _httpConfigured.GetAsync(reviewersUrl);
            reviewersResponse.EnsureSuccessStatusCode();
            var reviewersJson = JsonDocument.Parse(await reviewersResponse.Content.ReadAsStringAsync());

            foreach (var reviewer in reviewersJson.RootElement.GetProperty("value").EnumerateArray())
            {
                prHistory.Reviewers.Add(new PasadorScript.DTO.AzureDev.ReviewerInfo
                {
                    Name = reviewer.GetProperty("displayName").GetString()!,
                    Vote = reviewer.GetProperty("vote").GetInt32(),
                    UrlImage = reviewer.GetProperty("_links").GetProperty("avatar").GetProperty("href").GetString()!
                });
            }

            // Vote history (threads)
            var urlthreads = $"{Ctx(ProjectType).BaseUrl}/_apis/git/repositories/{repositoryId}/pullRequests/{prId}/threads?api-version=7.1-preview.1";
            var threadsResponse = await _httpConfigured.GetAsync(urlthreads);
            threadsResponse.EnsureSuccessStatusCode();
            var threadsJson = JsonDocument.Parse(await threadsResponse.Content.ReadAsStringAsync());

            foreach (var thread in threadsJson.RootElement.GetProperty("value").EnumerateArray())
            {
                foreach (var comment in thread.GetProperty("comments").EnumerateArray())
                {
                    if (comment.GetProperty("commentType").GetString() == "system")
                    {
                        var content = comment.GetProperty("content").GetString();
                        if (content!.Contains("voted"))
                        {
                            var user = comment.GetProperty("author").GetProperty("displayName").GetString();
                            var published = comment.GetProperty("publishedDate").GetDateTime();
                            var actionText = content.Split("voted")[1].Trim(); // Ej: "Approve on this pull request."

                            prHistory.VoteHistory.Add(new VoteEvent
                            {
                                User = user!,
                                Vote = int.Parse(actionText),
                                Timestamp = published
                            });
                        }
                    }
                }
            }
            foreach (var thread in threadsJson.RootElement.GetProperty("value").EnumerateArray())
            {
                foreach (var comment in thread.GetProperty("comments").EnumerateArray())
                {
                    var commentType = comment.GetProperty("commentType").GetString();
                    var user = comment.GetProperty("author").GetProperty("displayName").GetString();
                    var published = comment.GetProperty("publishedDate").GetDateTime();
                    var content = comment.GetProperty("content").GetString();

                    if (commentType == "system")
                    {
                        if (content!.Contains("voted"))
                        {

                            var actionText = content.Split("voted")[1].Trim(); // Ej: "Approve on this pull request."

                            prHistory.VoteHistory.Add(new VoteEvent
                            {
                                User = user!,
                                Vote = int.Parse(actionText),
                                Timestamp = published,
                                IsVote = true
                            });
                        }
                    }
                    else if (commentType == "text" || commentType == "codeChange")
                    {
                        prHistory.VoteHistory.Add(new VoteEvent
                        {
                            User = user!,
                            Timestamp = published,
                            Content = content!,
                            IsVote = false
                        });
                    }
                }
            }

            return prHistory;
        }


        public async Task<List<AzureDevopsPullRequest>> GetPullRequestsAsync(string repositoryId, string? status = null, int? top = null)
        {
            var prUrl = $"{Ctx(ProjectType).BaseUrl}/_apis/git/repositories/{repositoryId}/pullrequests?api-version=7.1-preview.1";
            var url = prUrl;
            if (status == null)
                status = "active";
            url += $"&searchCriteria.status={status}";
            if (top != null)
                url += $"&$top={top}";
            else
                url += $"&$top={int.Parse(_configuration["AzureSettings:TopPrDefault"]!)}";
            _httpConfigured.IgnoreCaseSensitive = true;
            var result = await _httpConfigured.GetAsync<PullRequestResponse>(url);
            return result?.Value ?? new List<AzureDevopsPullRequest>();
        }

        public async Task VoteOnPullRequestWithPatAsync(string pat, string AdName, string repositoryId, int pullRequestId, int voteValue, string? comments = null)
        {
            //using var httpClient = new HttpClient();
            var baseUrl = Ctx(ProjectType).BaseUrl;
            // Autenticación con PAT
            //var patToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
            //httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", patToken);

            // 1. Obtener el descriptor del usuario
            var meResponse = await _serviciosGraph.GetUserByAdName(pat, AdName);
            var reviewerDescriptor = meResponse?.Descriptor;
            var reviewerId = meResponse?.Id;

            if (reviewerDescriptor == null || reviewerId == null)
                throw new Exception("No se pudo obtener el descriptor del usuario asociado al PAT.");

            // 2. Verificar si ya es reviewer del PR
            var reviewersUrl = $"{baseUrl}/_apis/git/repositories/{repositoryId}/pullRequests/{pullRequestId}/reviewers?api-version=7.1-preview.1";
            //var reviewersResponse = await httpClient.GetFromJsonAsync<DTO.Graph.ReviewersListResponse>(reviewersUrl);
            var reviewersResponse = await _httpConfigured.GetAsync<DTO.Graph.ReviewersListResponse>(reviewersUrl, pat);
            var reviewer = reviewersResponse?.Value?.FirstOrDefault(r => r.Id == reviewerId);


            // Agrego el voto
            var addReviewerUrl = $"{baseUrl}/_apis/git/repositories/{repositoryId}/pullRequests/{pullRequestId}/reviewers/{reviewerId}?api-version=7.1-preview.1";
            var payload = new
            {
                id = reviewerId,
                //isRequired = true,
                vote = voteValue
            };
            var addResponse = await _httpUser.SendAsync(HttpMethod.Put, addReviewerUrl, payload, pat);
            //var addResponse = await httpClient.PutAsJsonAsync(addReviewerUrl, payload);
            var branchContent = await addResponse.Content.ReadAsStringAsync();
            addResponse.EnsureSuccessStatusCode();

            // 2. Si hay comentario, dejarlo como thread
            if (!string.IsNullOrWhiteSpace(comments))
            {
                var commentUrl = $"{baseUrl}/_apis/git/repositories/{repositoryId}/pullRequests/{pullRequestId}/threads?api-version=7.1-preview.1";

                var commentBody = new
                {
                    comments = new[]
                    {
                        new
                        {
                            content = comments,
                            commentType = "text"
                        }
                    },
                    status = "active",
                };

                var commentResponse = await _httpUser.SendAsync(HttpMethod.Post, commentUrl, commentBody, pat);
                commentResponse.EnsureSuccessStatusCode();
            }
        }

        
        //helpers
        private async Task<PRDetails> GetPRDetailsAsync(int pullRequestId)
        {
            var _baseUrl = Ctx(ProjectType).BaseUrl;
            var url = $"{_baseUrl}/_apis/git/pullrequests/{pullRequestId}?api-version=7.0";

            var response = await _httpConfigured.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<PRDetails>(json)!;
        }
        private async Task<List<PRFileChange>> GetPRFilesAlternativeAsync(int pullRequestId)
        {
            try
            {
                var _baseUrl = Ctx(ProjectType).BaseUrl;
                // Primero obtener el ID del repositorio
                var prDetails = await GetPRDetailsAsync(pullRequestId);
                var repositoryId = prDetails.Repository.Id;

                // Usar el endpoint de iterations
                var url = $"{_baseUrl}/_apis/git/repositories/{repositoryId}/pullrequests/{pullRequestId}/iterations?api-version=7.0";

                var response = await _httpConfigured.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var iterations = JsonSerializer.Deserialize<IterationsResponse>(json);

                if (iterations!.Value.Count == 0) return new List<PRFileChange>();

                // Obtener cambios de la última iteración
                var latestIterationId = iterations.Value[^1].Id;
                var changesUrl = $"{_baseUrl}/_apis/git/repositories/{repositoryId}/pullrequests/{pullRequestId}/iterations/{latestIterationId}/changes?api-version=7.0";

                var changesResponse = await _httpConfigured.GetAsync(changesUrl);
                changesResponse.EnsureSuccessStatusCode();

                var changesJson = await changesResponse.Content.ReadAsStringAsync();
                var changes = JsonSerializer.Deserialize<ChangesResponse>(changesJson);

                var files = new List<PRFileChange>();
                foreach (var change in changes!.ChangeEntries)
                {
                    if (change.Item != null && !string.IsNullOrEmpty(change.Item.Path))
                    {
                        files.Add(new PRFileChange
                        {
                            Path = change.Item.Path,
                            ChangeType = change.ChangeType,
                            ObjectId = change.Item.ObjectId
                        });
                    }
                }

                return files;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error obteniendo archivos: {ex.Message}");
                return new List<PRFileChange>();
            }
        }
        private async Task<FileChangeStats> GetFileChangeStatsAsync(string repositoryId, string sourceCommit, string targetCommit, string filePath)
        {
            var _baseUrl = Ctx(ProjectType).BaseUrl;
            var url = $"{_baseUrl}/_apis/git/repositories/{repositoryId}/diffs/commits";

            var queryParams = new Dictionary<string, string>
            {
                ["baseVersion"] = targetCommit,
                ["baseVersionType"] = "commit",
                ["targetVersion"] = sourceCommit,
                ["targetVersionType"] = "commit",
                ["diffCommonCommit"] = "false",
                ["path"] = filePath,
                ["api-version"] = "7.0"
            };

            var queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
            var fullUrl = $"{url}?{queryString}";

            var response = await _httpConfigured.GetAsync(fullUrl);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var diffResponse = JsonSerializer.Deserialize<DiffResponse>(json);

            return new FileChangeStats
            {
                Added = diffResponse!.ChangeCounts?.Add ?? 0,
                Deleted = diffResponse.ChangeCounts?.Delete ?? 0,
                Modified = diffResponse.ChangeCounts?.Edit ?? 0
            };
        }
        private async Task<FileDetailedDiff> GetFileDetailedDiffAsync(string repositoryId, string sourceCommit, string targetCommit, string filePath)
        {
            try
            {
                // Obtener el contenido de ambos commits
                var sourceContent = await _azureCommitService.GetFileContentAtCommit(repositoryId, sourceCommit, filePath);
                var targetContent = await _azureCommitService.GetFileContentAtCommit(repositoryId, targetCommit, filePath);

                // Generar el diff
                var diffLines = GenerateLineByLineDiff(sourceContent, targetContent, filePath);

                return new FileDetailedDiff
                {
                    FilePath = filePath,
                    DiffLines = diffLines,
                    AddedLines = diffLines.Count(l => l.LineType == LineType.Added),
                    DeletedLines = diffLines.Count(l => l.LineType == LineType.Deleted),
                    TotalChanges = diffLines.Count(l => l.LineType != LineType.Unchanged)
                };
            }
            catch (Exception ex)
            {
                return new FileDetailedDiff
                {
                    FilePath = filePath,
                    Error = ex.Message,
                    DiffLines = new List<DiffLine>()
                };
            }
        }
        private List<DiffLine> GenerateLineByLineDiff(string oldContent, string newContent, string filePath)
        {
            var diffLines = new List<DiffLine>();
            var oldLines = string.IsNullOrEmpty(oldContent) ? Array.Empty<string>() : oldContent.Split('\n');
            var newLines = string.IsNullOrEmpty(newContent) ? Array.Empty<string>() : newContent.Split('\n');

            int oldLineNumber = 1;
            int newLineNumber = 1;

            // Algoritmo simple de comparación (para producción usar una librería como DiffPlex)
            int i = 0, j = 0;
            while (i < oldLines.Length || j < newLines.Length)
            {
                if (i < oldLines.Length && j < newLines.Length && oldLines[i] == newLines[j])
                {
                    // Línea sin cambios
                    diffLines.Add(new DiffLine
                    {
                        LineType = LineType.Unchanged,
                        Content = oldLines[i],
                        OldLineNumber = oldLineNumber,
                        NewLineNumber = newLineNumber
                    });
                    i++;
                    j++;
                    oldLineNumber++;
                    newLineNumber++;
                }
                else if (j < newLines.Length && (i >= oldLines.Length || !ArrayContains(oldLines, newLines[j], i)))
                {
                    // Línea agregada
                    diffLines.Add(new DiffLine
                    {
                        LineType = LineType.Added,
                        Content = newLines[j],
                        NewLineNumber = newLineNumber
                    });
                    j++;
                    newLineNumber++;
                }
                else if (i < oldLines.Length && (j >= newLines.Length || !ArrayContains(newLines, oldLines[i], j)))
                {
                    // Línea eliminada
                    diffLines.Add(new DiffLine
                    {
                        LineType = LineType.Deleted,
                        Content = oldLines[i],
                        OldLineNumber = oldLineNumber
                    });
                    i++;
                    oldLineNumber++;
                }
                else
                {
                    // Línea modificada (tratamos como eliminada + agregada)
                    diffLines.Add(new DiffLine
                    {
                        LineType = LineType.Deleted,
                        Content = oldLines[i],
                        OldLineNumber = oldLineNumber
                    });
                    diffLines.Add(new DiffLine
                    {
                        LineType = LineType.Added,
                        Content = newLines[j],
                        NewLineNumber = newLineNumber
                    });
                    i++;
                    j++;
                    oldLineNumber++;
                    newLineNumber++;
                }
            }

            return diffLines;
        }

        private bool ArrayContains(string[] array, string value, int startIndex)
        {
            for (int k = startIndex; k < array.Length; k++)
            {
                if (array[k] == value)
                    return true;
            }
            return false;
        }
    }
}
