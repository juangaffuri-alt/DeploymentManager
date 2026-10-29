using Microsoft.Extensions.Options;
using PasadorScript.DbModels;
using PasadorScript.DTO.AzureDev;
using PasadorScript.DTO.Git;
using PasadorScript.Extensions;
using PasadorScript.Services.Azure;
using PasadorScript.Services.Graph;
using PasadorScript.Services.User;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GitCommit = PasadorScript.DTO.Git.GitCommit;

namespace PasadorScript.Services.Git
{
    public class GitService : IGitService
    {
        private HttpClient _httpClient { get; set; }
        private readonly IConfiguration _configuration;
        private readonly IAzureService _serviciosAzure;
        private readonly ILogger<GitService> _logger;
        private readonly IUserContext _userContext;
        private readonly IServiciosGraph _serviciosGraph;


        public string? ProjectDevops { get; set; }
        public string? RepositoryId { get; set; }

        public GitService(IConfiguration config, ILogger<GitService> logger, IAzureService serviciosAzure,
            IUserContext userContext, IServiciosGraph serviciosGraph)
        {
            _configuration = config;
            _serviciosAzure = serviciosAzure;
            _logger = logger;
            _userContext = userContext;
            _serviciosGraph = serviciosGraph;
            _httpClient = new HttpClient();
            var activePat = string.IsNullOrEmpty(_userContext.PersonalAccessToken) ? _configuration["AzureSettings:PersonalAccessToken"]! : _userContext.PersonalAccessToken;
            // Autenticación con PAT
            var patToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{activePat}"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", patToken);
        }

        

        public async Task<MergeRevertResult> RevertMergeAndCreatePr(int completedPullRequestId, RevertStrategy strategy)
        {
            var result = new MergeRevertResult();

            try
            {
                _logger.LogInformation("Iniciando revert del PR {PullRequestId}", completedPullRequestId);

                // 1. Obtener detalles del PR completado
                var prDetails = await _serviciosAzure.GetPullRequestAsync(completedPullRequestId);

                if (prDetails!.Status != "completed")
                {
                    result.ErrorMessage = "Solo se pueden revertir PRs completados";
                    return result;
                }

                // 2. Obtener el merge commit del PR
                var mergeCommit = await GetMergeCommit(completedPullRequestId);
                if (mergeCommit == null)
                {
                    result.ErrorMessage = "No se pudo encontrar el merge commit del PR";
                    return result;
                }

                _logger.LogInformation("Merge commit encontrado: {CommitId}", mergeCommit.CommitId);

                // 3. **ENFOQUE SIMPLIFICADO**: Crear rama desde el primer parent del merge commit
                // Esto esencialmente "revierte" al estado anterior al merge
                var newBranchName = $"{strategy.NewBranchPrefix}/pr-{completedPullRequestId}-{DateTime.Now:yyyyMMdd-HHmmss}";

                // El primer parent del merge commit es el estado de la rama target antes del merge
                var baseCommitId = mergeCommit.Parents.FirstOrDefault();
                if (string.IsNullOrEmpty(baseCommitId))
                {
                    result.ErrorMessage = "No se pudo obtener el commit base para el revert";
                    return result;
                }

                // 4. Crear nueva rama desde el estado anterior al merge
                await CreateBranchFromCommit(newBranchName, baseCommitId);

                _logger.LogInformation("Nueva rama creada: {BranchName} desde commit {CommitId}", newBranchName, baseCommitId);

                // 5. Crear nuevo Pull Request
                var newPrId = await CreatePullRequestFromRevert(newBranchName, prDetails.TargetBranch, completedPullRequestId);

                result.Success = true;
                result.NewCommitId = baseCommitId; // Usamos el base commit como referencia
                result.NewBranchName = newBranchName;
                result.NewPullRequestId = newPrId;

                _logger.LogInformation("✅ Revert completado exitosamente. Nuevo PR: {NewPrId}", newPrId);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en revert del PR {PullRequestId}", completedPullRequestId);
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        public async Task<GitCommit> GetMergeCommit(int pullRequestId)
        {
            try
            {
                // 1. Obtener los detalles del PR que SÍ incluyen el merge commit
                var prDetails = await _serviciosAzure.GetPullRequestAsync(pullRequestId);

                if (prDetails?.LastMergeCommit == null ||
                    string.IsNullOrEmpty(prDetails.LastMergeCommit.CommitId))
                {
                    _logger.LogWarning("PR {PullRequestId} no tiene lastMergeCommit", pullRequestId);
                    return null;
                }

                var mergeCommitId = prDetails.LastMergeCommit.CommitId;
                _logger.LogInformation("Merge commit ID: {MergeCommitId}", mergeCommitId);

                var baseUrl = GetUriFormatted("baseUrl");
                // 2. Obtener los detalles del MERGE COMMIT (no de los commits regulares)
                var commitUrl = $"{baseUrl}/_apis/git/repositories/" +
                               $"{RepositoryId}/commits/{mergeCommitId}?api-version=7.0";

                var response = await _httpClient.GetAsync(commitUrl);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Error obteniendo merge commit: {StatusCode}", response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Merge commit details: {Content}", content);

                using var doc = JsonDocument.Parse(content);
                var commitElement = doc.RootElement;

                // 3. Crear el GitCommit - el MERGE COMMIT sí tendrá parents
                return new GitCommit
                {
                    CommitId = commitElement.GetProperty("commitId").GetString()!,
                    Message = commitElement.GetProperty("comment").GetString()!,
                    Author = commitElement.GetProperty("author").GetProperty("name").GetString()!,
                    Date = DateTimeOffset.Parse(commitElement.GetProperty("author").GetProperty("date").GetString()!),
                    Parents = commitElement.GetProperty("parents").EnumerateArray()!
                                .Select(p => p.GetString()!).ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo merge commit del PR {PullRequestId}", pullRequestId);
                return null;
            }
        }



        public async Task<string> CreateRevertCommit(string mergeCommitId, string revertMessage)
        {
            try
            {
                var baseUrl = GetUriFormatted("baseUrl");

                // Crear una rama temporal para el revert
                var tempBranchName = $"revert-{DateTime.Now:yyyyMMdd-HHmmss}";

                // Obtener el primer parent del merge commit (estado anterior)
                var commitUrl = $"{baseUrl}/_apis/git/repositories/" +
                               $"{RepositoryId}/commits/{mergeCommitId}?api-version=7.0";

                var commitResponse = await _httpClient.GetAsync(commitUrl);
                var commitContent = await commitResponse.Content.ReadAsStringAsync();
                using var commitDoc = JsonDocument.Parse(commitContent);

                var baseCommitId = commitDoc.RootElement.GetProperty("parents")[0].GetString();

                // Crear rama desde el estado anterior al merge
                await CreateBranchFromCommit(tempBranchName, baseCommitId);

                // **USAR MERGE CON EL PRIMER PARENT - esto esencialmente revierte**
                var mergeRequest = new
                {
                    parents = new[] { baseCommitId }, // El estado anterior
                    committedDate = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    comment = revertMessage,
                    author = new
                    {
                        name = "Revert Service",
                        email = "revert@service.com",
                        date = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                    }
                };

                var mergeUrl = $"{baseUrl}/_apis/git/repositories/" +
                              $"{RepositoryId}/commits/{baseCommitId}?api-version=7.0";

                var response = await _httpClient.PatchAsJsonAsync(mergeUrl, mergeRequest);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(content);

                    var newCommitId = doc.RootElement.GetProperty("commitId").GetString();
                    _logger.LogInformation("Revert creado: {CommitId}", newCommitId);

                    return newCommitId;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Error en revert: {Error}", errorContent);

                    // Si falla, intentemos con el approach más simple: crear un nuevo commit vacío
                    return await CreateSimpleRevertCommit(tempBranchName, baseCommitId, revertMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CreateRevertCommit");
                return null;
            }
        }

        private async Task<string> CreateSimpleRevertCommit(string branchName, string targetCommitId, string revertMessage)
        {
            try
            {
                var baseUrl = GetUriFormatted("baseUrl");

                // Enfoque más simple: crear un commit vacío que apunte al estado anterior
                var pushRequest = new
                {
                    refUpdates = new[]
                    {
                new
                {
                    name = $"refs/heads/{branchName}",
                    oldObjectId = await GetCurrentBranchCommit(branchName),
                    newObjectId = targetCommitId
                }
            },
                    commits = new[]
                    {
                new
                {
                    comment = revertMessage,
                    changes = new object[0] // Sin cambios
                }
            }
                };

                var pushUrl = $"{baseUrl}/_apis/git/repositories/" +
                             $"{RepositoryId}/pushes?api-version=7.0";

                var response = await _httpClient.PostAsJsonAsync(pushUrl, pushRequest);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(content);
                    return doc.RootElement.GetProperty("commits")[0].GetProperty("commitId").GetString()!;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CreateSimpleRevertCommit");
                return null;
            }
        }

        public async Task<string> CreateBranchFromCommit(string branchName, string commitId)
        {
            try
            {
                var baseUrl = GetUriFormatted("baseUrl");
                var refUpdate = new[]
                {
                new
                {
                    name = $"refs/heads/{branchName}",
                    oldObjectId = "0000000000000000000000000000000000000000",
                    newObjectId = commitId
                }
            };

                var url = $"{baseUrl}/_apis/git/repositories/" +
                          $"{RepositoryId}/refs?api-version=7.0";

                var response = await _httpClient.PostAsJsonAsync(url, refUpdate);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);

                return doc.RootElement.GetProperty("value")[0].GetProperty("name").GetString()!;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando branch {BranchName} from commit {CommitId}",
                    branchName, commitId);
                throw;
            }
        }

        public async Task<int> CreatePullRequestFromRevert(string sourceBranch, string targetBranch, int originalPrId)
        {
            try
            {
                List<string> revIds = [];
                var iycRv = _configuration.GetReviewerConfig("IyC");
                revIds.Add(iycRv.FirstOrDefault()!.Id);

                // 3. FORMATEAR CORRECTAMENTE LAS RAMAS
                var sourceRef = sourceBranch.StartsWith("refs/heads/") ? sourceBranch : $"refs/heads/{sourceBranch}";
                var targetRef = targetBranch.StartsWith("refs/heads/") ? targetBranch : $"refs/heads/{targetBranch}";


                var baseUrl = GetUriFormatted("baseUrl");
                var prRequest = new
                {
                    sourceRefName = sourceRef,
                    targetRefName = targetRef,
                    title = $"Revert: PR #{originalPrId}",
                    description = $"This PR reverts the changes from PR #{originalPrId}\n\n" +
                                 $"**Revert created automatically**\n" +
                                 $"Original PR: #{originalPrId}\n" +
                                 $"Revert date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    reviewers = new object[0] // Array vacío
                };

                var url = $"{baseUrl}/_apis/git/repositories/" +
                          $"{RepositoryId}/pullrequests?api-version=7.0";

                var response = await _httpClient.PostAsJsonAsync(url, prRequest);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);

                var newPrId = doc.RootElement.GetProperty("pullRequestId").GetInt32();

                if (bool.Parse(_configuration["Testing:IncludeReviewersGx"]!))
                {
                    await AddReviewerToPR(newPrId, revIds.ToArray());

                }

                if (bool.Parse(_configuration["Testing:AutocompleteGX"]!))
                {
                    await EnableAutoCompleteForPR(newPrId);
                }

                return newPrId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando PR desde revert {SourceBranch} -> {TargetBranch}",
                    sourceBranch, targetBranch);
                throw;
            }
        }


        public async Task<bool> CanRevertPullRequest(int pullRequestId)
        {
            try
            {
                var prDetails = await _serviciosAzure.GetPullRequestAsync(pullRequestId);

                // Verificar que el PR está completado
                if (!prDetails.IsCompleted)
                {
                    _logger.LogInformation("PR {PullRequestId} no está completado", pullRequestId);
                    return false;
                }

                // Verificar que tiene merge commit
                if (prDetails.LastMergeCommit == null ||
                    string.IsNullOrEmpty(prDetails.LastMergeCommit.CommitId))
                {
                    _logger.LogInformation("PR {PullRequestId} no tiene lastMergeCommit", pullRequestId);
                    return false;
                }

                // Verificar que el merge commit es un verdadero merge (tiene 2+ parents)
                var isMerge = await IsMergeCommit(prDetails.LastMergeCommit.CommitId);

                _logger.LogInformation("PR {PullRequestId} puede revertir: {CanRevert}",
                    pullRequestId, isMerge);

                return isMerge;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CanRevertPullRequest para PR {PullRequestId}", pullRequestId);
                return false;
            }
        }


        protected string GetUriFormatted(string keyValueInEndpointSetting, params string[] args)
        {
            string host = _configuration["AzureSettings:Host"]!;
            string organization = Uri.EscapeDataString(_configuration["AzureSettings:Organization"]!);
            string project = ProjectDevops!;

            string formattedUrl = _configuration[$"AzureSettings:Endpoints:{keyValueInEndpointSetting}"]!;
            object[] finalArgs = new object[] { host, organization, project }
            .Concat(args.Cast<object>()) // args debe ser de tipo object[] o algo que puedas castear
            .ToArray();

            // Aplicamos el formato con todos los argumentos
            string res = string.Format(formattedUrl, finalArgs);
            return res;

        }

        public async Task<bool> IsMergeCommit(string commitId)
        {
            try
            {
                // Obtener los commits del PR
                var baseUrl = GetUriFormatted("baseUrl");
                var commitUrl = $"{baseUrl}/_apis/git/repositories/" +
                               $"{RepositoryId}/commits/{commitId}?api-version=7.0";

                var response = await _httpClient.GetAsync(commitUrl);
                if (!response.IsSuccessStatusCode) return false;

                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                var commitElement = doc.RootElement;

                // Un merge commit tiene 2 o más parents
                if (commitElement.TryGetProperty("parents", out var parents) &&
                    parents.ValueKind == JsonValueKind.Array)
                {
                    var parentCount = parents.GetArrayLength();
                    _logger.LogInformation("Commit {CommitId} tiene {ParentCount} parents", commitId, parentCount);
                    return parentCount >= 2;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verificando si {CommitId} es merge commit", commitId);
                return false;
            }
        }
        private async Task<string> GetCurrentBranchCommit(string branchName)
        {
            try
            {
                var baseUrl = GetUriFormatted("baseUrl");
                var url = $"{baseUrl}/_apis/git/repositories/" +
                          $"{RepositoryId}/refs/heads/{branchName}?api-version=7.0";

                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(content);
                    return doc.RootElement.GetProperty("value")[0].GetProperty("objectId").GetString()!;
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo commit actual de {BranchName}", branchName);
                return null;
            }
        }

        public async Task<RecreationResult> RecreatePullRequest(int completedPullRequestId)
        {
            try
            {
                _logger.LogInformation("Iniciando recreación del PR {PullRequestId}", completedPullRequestId);

                // 1. Obtener detalles del PR original
                var originalPr = await _serviciosAzure.GetPullRequestAsync(completedPullRequestId);

                if (!originalPr!.IsCompleted)
                {
                    return new RecreationResult { ErrorMessage = "Solo se pueden recrear PRs completados" };
                }

                // 2. Obtener el PRER MERGE COMMIT - el último commit del branch original ANTES del merge
                var lastSourceCommit = await GetLastSourceCommitBeforeMerge(completedPullRequestId);
                if (lastSourceCommit == null)
                {
                    return new RecreationResult { ErrorMessage = "No se pudo encontrar el último commit del branch original" };
                }

                _logger.LogInformation("Último commit del branch original: {CommitId}", lastSourceCommit.CommitId);

                // 3. Crear NUEVA rama desde el último commit del branch ORIGINAL
                var newBranchName = $"recreate-pr-{completedPullRequestId}-{DateTime.Now:yyyyMMdd-HHmmss}";
                await CreateBranchFromCommit(newBranchName, lastSourceCommit.CommitId);

                _logger.LogInformation("Nueva rama creada: {BranchName} desde commit {CommitId}",
                    newBranchName, lastSourceCommit.CommitId);

                // 4. Crear NUEVO PR
                var newPrId = await CreateRecreatedPullRequest(newBranchName, originalPr.TargetBranch, originalPr);

                return new RecreationResult
                {
                    Success = true,
                    NewBranchName = newBranchName,
                    NewPullRequestId = newPrId,
                    OriginalCommitId = lastSourceCommit.CommitId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recreando PR {PullRequestId}", completedPullRequestId);
                return new RecreationResult { ErrorMessage = ex.Message };
            }
        }

        private async Task<GitCommit> GetLastSourceCommitBeforeMerge(int pullRequestId)
        {
            var originalPr = await _serviciosAzure.GetPullRequestAsync(pullRequestId);

            // El lastMergeSourceCommit es el último commit del branch source antes del merge
            if (originalPr?.LastMergeSourceCommit != null &&
                !string.IsNullOrEmpty(originalPr.LastMergeSourceCommit.CommitId))
            {
                return new GitCommit
                {
                    CommitId = originalPr.LastMergeSourceCommit.CommitId,
                    Message = "Last source commit before merge"
                };
            }

            // Fallback: obtener de los commits del PR
            return await GetLastCommitFromPullRequestCommits(pullRequestId);
        }

        private async Task<GitCommit> GetLastCommitFromPullRequestCommits(int pullRequestId)
        {
            try
            {
                var baseUrl = GetUriFormatted("baseUrl");

                // Obtener todos los commits del PR
                var commitsUrl = $"{baseUrl}/_apis/git/repositories/{RepositoryId}/pullrequests/" +
                                $"{pullRequestId}/commits?api-version=7.0";

                var response = await _httpClient.GetAsync(commitsUrl);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);

                var commitsArray = doc.RootElement.GetProperty("value");

                // Verificar que hay commits
                if (commitsArray.GetArrayLength() == 0)
                {
                    _logger.LogWarning("No se encontraron commits en el PR {PullRequestId}", pullRequestId);
                    return null;
                }

                // El ÚLTIMO elemento del array es el último commit del PR
                var lastCommitElement = commitsArray[commitsArray.GetArrayLength() - 1];

                var lastCommit = new GitCommit
                {
                    CommitId = lastCommitElement.GetProperty("commitId").GetString()!,
                    Message = lastCommitElement.GetProperty("comment").GetString()!
                };

                // Opcional: obtener autor y fecha
                if (lastCommitElement.TryGetProperty("author", out var author))
                {
                    lastCommit.Author = author.GetProperty("name").GetString()!;
                    if (author.TryGetProperty("date", out var date))
                    {
                        lastCommit.Date = DateTimeOffset.Parse(date.GetString()!);
                    }
                }

                _logger.LogInformation("Último commit del PR {PullRequestId}: {CommitId}",
                    pullRequestId, lastCommit.CommitId);

                return lastCommit;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo último commit del PR {PullRequestId}", pullRequestId);
                return null;
            }
        }

        private async Task<int> CreateRecreatedPullRequest(string sourceBranch, string targetBranch, AzureDevopsPullRequest originalPr)
        {
            var baseUrl = GetUriFormatted("baseUrl");

            List<string> revIds = [];
            var iycRv = _configuration.GetReviewerConfig("IyC");
            revIds.Add(iycRv.FirstOrDefault()!.Id);

            var sourceRef = sourceBranch.StartsWith("refs/heads/") ? sourceBranch : $"refs/heads/{sourceBranch}";
            var targetRef = targetBranch.StartsWith("refs/heads/") ? targetBranch : $"refs/heads/{targetBranch}";

            var prRequest = new
            {
                sourceRefName = sourceRef,
                targetRefName = targetRef,
                title = $"Recreación: {originalPr.Title}",
                description = $"Este PR recrea el PR original #{originalPr.PullRequestId}\n\n" +
                             $"**PR original:** #{originalPr.PullRequestId}\n" +
                             $"**Título original:** {originalPr.Title}\n" +
                             $"**Fecha original:** {originalPr.CreationDate:yyyy-MM-dd HH:mm:ss}\n" +
                             $"**Recreado el:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n" +
                             $"{originalPr.Description}",
                // Sin reviewers - se asignarán automáticamente
            };

            var url = $"{baseUrl}/_apis/git/repositories/{RepositoryId}/pullrequests?api-version=7.0";
            var response = await _httpClient.PostAsJsonAsync(url, prRequest);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);

            var newPrId = doc.RootElement.GetProperty("pullRequestId").GetInt32();

            if (bool.Parse(_configuration["Testing:IncludeReviewersGx"]!))
            {
                await AddReviewerToPR(newPrId, revIds.ToArray());

            }

            if (bool.Parse(_configuration["Testing:AutocompleteGX"]!))
            {
                await EnableAutoCompleteForPR(newPrId);
            }

            return newPrId;
        }

        public async Task<RecreationResult> RecreatePullRequestManual(int completedPullRequestId)
        {
            try
            {
                var originalPr = await _serviciosAzure.GetPullRequestAsync(completedPullRequestId);

                // 1. Obtener los archivos específicos que cambiaron en el PR original
                var changedFiles = await GetChangedFilesFromPullRequest(completedPullRequestId);
                if (!changedFiles.Any())
                {
                    return new RecreationResult { ErrorMessage = "No se encontraron archivos cambiados en el PR original" };
                }

                _logger.LogInformation("Archivos cambiados en PR original: {Files}",
                    string.Join(", ", changedFiles));

                // 2. Crear nueva rama desde main actual
                var newBranchName = $"recreate-pr-{completedPullRequestId}-{DateTime.Now:yyyyMMdd-HHmmss}";
                var currentMainCommit = await GetCurrentBranchCommit("main");
                await CreateBranchFromCommit(newBranchName, currentMainCommit);

                // 3. Para cada archivo cambiado, obtener su contenido del PR original y recrearlo
                foreach (var file in changedFiles)
                {
                    var fileContent = await GetFileContentFromPullRequest(completedPullRequestId, file);
                    var nFile = file.StartsWith('/') ? file.TrimStart('/') : file;
                    var split = nFile.Split("-");
                    var ext = System.IO.Path.GetExtension(nFile);
                    var newNFileName = nFile;
                    if (split.Length > 1)
                    {
                        newNFileName = "";
                        for (int i = 0; i < split.Length - 1; i++)
                        {
                            newNFileName += $"{split[i]}-";
                        }
                        newNFileName += DateTime.Now.ToString("yyyyMMddHHmmss");
                        newNFileName += ext;
                        
                    }
                    if (fileContent != null)
                    {
                        await CreateOrUpdateFileInBranch(newBranchName, newNFileName, fileContent,
                            $"Recrear {file} desde PR #{completedPullRequestId}");
                    }
                }

                // 4. Crear nuevo PR
                var newPrId = await CreateRecreatedPullRequest(newBranchName, "main", originalPr);

                return new RecreationResult
                {
                    Success = true,
                    NewBranchName = newBranchName,
                    NewPullRequestId = newPrId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recreando PR {PullRequestId} manualmente", completedPullRequestId);
                return new RecreationResult { ErrorMessage = ex.Message };
            }
        }

        private async Task<List<string>> GetChangedFilesFromPullRequest(int pullRequestId)
        {
            try
            {
                var baseUrl = GetUriFormatted("baseUrl");
                var url = $"{baseUrl}/_apis/git/repositories/{RepositoryId}/pullrequests/" +
                         $"{pullRequestId}/iterations/1/changes?api-version=7.0";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return new List<string>();

                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);

                var files = new List<string>();
                if (doc.RootElement.TryGetProperty("changeEntries", out var changes))
                {
                    foreach (var change in changes.EnumerateArray())
                    {
                        if (change.TryGetProperty("item", out var item) &&
                            item.TryGetProperty("path", out var path))
                        {
                            files.Add(path.GetString()!);
                        }
                    }
                }
                return files;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo archivos cambiados del PR {PullRequestId}", pullRequestId);
                return new List<string>();
            }
        }

        private async Task<string> GetFileContentFromPullRequest(int pullRequestId, string filePath)
        {
            var nFile = filePath.StartsWith('/') ? filePath.TrimStart('/') : filePath;
            try
            {
                var encodedPath = Uri.EscapeDataString(nFile);
                var urlBase = GetUriFormatted("baseUrl");
                var url = $"{urlBase}/_apis/git/repositories/{RepositoryId}/items?path={encodedPath}&download=true&api-version=7.1";

                var response = await _httpClient.GetAsync(url);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("File not found: {FilePath}", filePath);
                    return null;
                }

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file content for {FilePath}", filePath);
                return null;
            }
        }

        private async Task CreateOrUpdateFileInBranch(string branchName, string filePath, string content, string commitMessage)
        {
            try
            {
                var baseUrl = GetUriFormatted("baseUrl");

                var pushRequest = new
                {
                    refUpdates = new[]
                    {
                        new
                        {
                            name = $"refs/heads/{branchName}",
                            oldObjectId = await GetCurrentBranchCommit(branchName),
                            //newObjectId = await GetCurrentBranchCommit(branchName) // Mismo por ahora
                        }
                    },
                    commits = new[]
                    {
                        new
                        {
                            comment = commitMessage,
                            changes = new[]
                            {
                                new
                                {
                                    changeType = "add", // o "edit" si existe
                                    item = new { path = filePath },
                                    newContent = new
                                    {
                                        content = content,
                                        contentType = "rawtext"
                                    }
                                }
                            }
                        }
                    }
                };

                var url = $"{baseUrl}/_apis/git/repositories/{RepositoryId}/pushes?api-version=7.0";
                await _httpClient.PostAsJsonAsync(url, pushRequest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando/actualizando archivo {File} en branch {Branch}",
                    filePath, branchName);
                throw;
            }
        }

        public async Task AddReviewerToPR(int pullRequestId, params string[] reviewerDescriptors)
        {
            var baseUrl = GetUriFormatted("BaseUrl");
            foreach (var reviewerDescriptor in reviewerDescriptors)
            {
                var putUrl = $"{baseUrl}/_apis/git/repositories/{RepositoryId}/pullRequests/{pullRequestId}/reviewers/{reviewerDescriptor}?api-version=7.1-preview.1";
                var payload = new
                {
                    id = reviewerDescriptor,
                    isRequired = true,
                    vote = 0
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(putUrl, content);
                var result = await response.Content.ReadAsStringAsync();

                _logger.LogDebug($"AddReviewerToPR Status: {response.StatusCode}. {result}");
                
            }
        }

        public async Task EnableAutoCompleteForPR(int pullRequestId, bool deleteSourceBranch = true, string mergeStrategy = "merge")
        {
            var patUsr = _userContext.PersonalAccessToken!;
            var myIds = await _serviciosGraph.GetMyIdAsAsync(patUsr);
            var prInfo = await _serviciosAzure.GetPullRequestAsync(pullRequestId);
            var idAutoComplete = myIds.Id;
            if (prInfo != null && prInfo.CreatedBy != null && !String.IsNullOrEmpty(prInfo.CreatedBy.Id))
                idAutoComplete = prInfo.CreatedBy.Id;

            var baseUrl = GetUriFormatted("baseUrl");
            var url = $"{baseUrl}/_apis/git/repositories/{RepositoryId}/pullRequests/{pullRequestId}?api-version=7.1-preview.1";
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
                    mergeCommitMessage = $"Auto-completed PR #{pullRequestId}"
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PatchAsync(url, content); // PATCH para actualizar el PR

            var result = await response.Content.ReadAsStringAsync();
            _logger.LogDebug($"AutoComplete Status: {response.StatusCode}");
            _logger.LogDebug(result);
        }
    }
}
