using AspNetCoreGeneratedDocument;
using Azure.Core;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PasadorScript.DbModels;
using PasadorScript.DTO.AzureDev;
using PasadorScript.DTO.Git;
using PasadorScript.Extensions;
using PasadorScript.Helpers;
using PasadorScript.Repository;
using PasadorScript.Services.Azure;
using PasadorScript.Services.Cache;
using PasadorScript.Services.Git;
using PasadorScript.ViewModels;
using System.Xml.Linq;

namespace PasadorScript.Controllers
{
    [Authorize]
    public class PullRequestController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IDbRepository _dbRepository;
        private readonly IMapper _mapper;
        private readonly TempDataHelper _tempDataHelper;
        private readonly IGitService _gitRevertService;
        private readonly ILogger<PullRequestController> _logger;
        private readonly IAzureService _azureService;
        private readonly IDevOpsCacheService _cacheService;

        public PullRequestController(IConfiguration configuration, IDbRepository dbRepository, IMapper mapper, TempDataHelper tempDataHelper,
            IGitService gitRevertService, ILogger<PullRequestController> logger, IAzureService azureService, IDevOpsCacheService cacheService)
        {
            _configuration = configuration;
            _dbRepository = dbRepository;
            _mapper = mapper;
            _tempDataHelper = tempDataHelper;
            _gitRevertService = gitRevertService;
            _logger = logger;
            _azureService = azureService;
            _cacheService = cacheService;
        }
        [HttpGet]
        public async Task<ActionResult<BuildStatusResult?>> GetStatus(int prId, string projectType = "Default", bool forceRefresh = false)
        {
            try
            {
                _azureService.ForceProyectType(projectType.ToAzureProjectType());
                var result = await _cacheService.GetOrCreatePullRequestStatusAsync(
                    prId,
                    async () => await _azureService.GetCompleteInfoPullRequestAndBuild(prId),
                    forceRefresh
                );

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error obteniendo estado PR {prId}");
                return Json(new BuildStatusResult { PullRequestStatus = "error" });
            }
        }

        [HttpGet]
        public async Task<ActionResult<ReleaseDto?>> GetReleasesSctiptsByPr(int prId, string projectType = "Default", bool forceRefresh = false)
        {
            try
            {
                _azureService.ForceProyectType(projectType.ToAzureProjectType());
                var result = await _cacheService.GetOrCreatePullRequestReleasesAsync(
                    prId,
                    async () => await _azureService.ObtenerReleaseDesdePullRequestAsync(prId),
                    forceRefresh
                );

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error obteniendo releases PR {prId}");
                return Json(new ReleaseDto());
            }
        }

        // Agregar este método para limpiar caché manualmente
        [HttpPost]
        public IActionResult ClearCache(int prId = 0)
        {
            if (prId > 0)
            {
                _cacheService.InvalidatePullRequestCache(prId);
                return Json(new { success = true, message = $"Cache limpiada para PR {prId}" });
            }
            else
            {
                _cacheService.InvalidateAllCache();
                return Json(new { success = true, message = "Cache global limpiada" });
            }
        }
        [HttpGet]
        public async Task<ActionResult<BuildStatusResult?>> GetStatusGx(int prId, string projectType, string repositoryId, bool forceRefresh = false)
        {
            try
            {
                var pt = projectType.ToAzureProjectType();
                var repos = _configuration.GetSection("Projects").Get<List<ProjectSetting>>()?.Where(x => x.IsGx && x.Active)?.FirstOrDefault()?.Repos;
                var repo = repos.FirstOrDefault(x => x.RepoName == repositoryId);
                _azureService.ForceProyectType(pt);
                var result = await _cacheService.GetOrCreatePullRequestStatusGxAsync(
                    prId, repositoryId,
                    async () => await _azureService.GetCompleteInfoPullRequestAndBuild(prId, repo!.BuildDefinitionId ?? 0),
                    forceRefresh
                );

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error obteniendo estado PR {prId}");
                return Json(new BuildStatusResult { PullRequestStatus = "error" });
            }
        }

        [HttpGet]
        public async Task<ActionResult<string>> GetLogBuild(int idPr, bool ownBuilds = true)
        {
            var commitId = await _azureService.GetMergeCommitIdAsync(idPr);
            var statuses = await _azureService.GetCommitStatusesAsync(_configuration["AzureSettings:RepositoryGx"]!, commitId!);
            var logs = "No se han encontrado resultados";
            if (ownBuilds)
            {
                var buildGx = statuses.FirstOrDefault(x => x.Description.Contains(_configuration["AzureSettings:PipelineBuildNameGx"]!));

                if (buildGx != null)
                {
                    logs = await _azureService.GetLogForTaskAsync(buildGx.BuildId.HasValue ? buildGx.BuildId.Value : 0, _configuration["AzureSettings:PipelineBuildTaskName"]!);
                }
            }
            else
            {
                logs = await _azureService.GetLogForTaskAsync(statuses.FirstOrDefault()!.BuildId!.Value, "");
            }

            return Json(new { log = logs });
        }

        [HttpGet]
        public async Task<ActionResult<ReleaseDto?>> GetReleasesByPr(int prId, string projectType, bool forceRefresh = false)
        {
            try
            {
                _azureService.ForceProyectType(projectType.ToAzureProjectType());
                var result = await _cacheService.GetOrCreatePullRequestReleasesGxAsync(
                    prId,
                    async () => await _azureService.ObtenerReleaseDesdePullRequestAsync(prId),
                    forceRefresh
                );

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error obteniendo releases PR {prId}");
                return Json(new ReleaseDto());
            }
        }


        [HttpGet]
        public async Task<ActionResult> TimeLine(bool gx, int id)
        {
            var model = await _dbRepository.IdToGenericModel(gx, id);
            if (model == null)
                return NotFound();
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ViewLogs(int prId, string projectType, bool gx)
        {
            BuildTimelineViewModel? model = null;
            _azureService.ForceProyectType(projectType.ToAzureProjectType());
            var build = await _azureService.GetBuildByPullRequestAsync(prId, null);
            if (build != null)
            {
                var tasks = await _azureService.GetBuildTimelineAsync(build.Id);

                model = new BuildTimelineViewModel
                {
                    BuildId = build.Id,
                    AzureProjectType = projectType,
                    Tasks = tasks.OrderBy(t => t.StartTime ?? DateTime.MinValue).ToList()
                };
            }

            return PartialView("_LogsModal", model);
        }

        // Devuelve solo el contenido de un log (texto plano)

        public async Task<IActionResult> GetLog(int buildId, int logId, string projectType = "Genexus")
        {
            _azureService.ForceProyectType(projectType.ToAzureProjectType());
            var log = await _azureService.GetLogContentAsync(buildId, logId);
            return Content(log ?? string.Empty, "text/plain; charset=utf-8");
        }

        // (opcional) endpoint JSON con timeline
        public async Task<IActionResult> GetTimelineJson(int buildId)
        {
            _azureService.ForceProyectType(AzureProjectType.GeneXus);
            var tasks = await _azureService.GetBuildTimelineAsync(buildId);
            return Ok(tasks);
        }

        [HttpGet]
        public async Task<IActionResult> GetDiff(int pullRequestId)
        {
            var prData = await _azureService.GetPRCompleteDataWithDiffsAsync(570);

            if (prData.Success)
            {
                // Generar vista tipo portal con diffs
                var portalView = _azureService.GeneratePortalLikeView(prData);

                // También puedes acceder a diffs individuales
                foreach (var filePath in prData.FileDiffs.Keys)
                {
                    var fileDiff = prData.FileDiffs[filePath];
                    var diffView = _azureService.GenerateFileDiffView(fileDiff);


                }
            }

            return Ok();
        }

        private async Task<string> GetModifiedContentFromPR(string prId, string commitId, string filePath)
        {
            // Obtener contenido original y modificado
            var content = await _azureService.GetFileContentAtCommit(
                prId,
                commitId,
                filePath
            );
            return content ?? "";
        }

        [HttpPost]
        public async Task<IActionResult> MonacoDiff(
    [FromForm] int prId,
    [FromForm] string filePath,
    [FromForm] string contentOrig)
        {
            try
            {
                var prData = await _azureService.GetPRCompleteDataWithDiffsAsync(prId);

                if (!prData.Success || !prData.FileDiffs.ContainsKey(filePath))
                {
                    var m = new MonacoDiffModel
                    {
                        PRId = prId,
                        FilePath = filePath,
                        FileName = Path.GetFileName(filePath),
                        ModifiedContent = contentOrig
                    };
                    return PartialView("_NoContentModal", m);
                }

                var fileDiff = prData.FileDiffs[filePath];

                // Obtener contenido original del PR
                var originalContent = await _azureService.GetFileContentAtCommit(
                    prData.PRDetails.Repository.Id,
                    prData.PRDetails.LastMergeTargetCommit.CommitId,
                    filePath
                );

                var model = new MonacoDiffModel
                {
                    PRId = prId,
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    OriginalContent = originalContent ?? "",
                    ModifiedContent = contentOrig ?? "",
                    DiffLines = fileDiff.DiffLines,
                    Language = GetLanguageFromExtension(filePath),
                    AddedLines = fileDiff.AddedLines,
                    DeletedLines = fileDiff.DeletedLines
                };

                // FORZAR vista sin layout para iframe
                return PartialView("_MonacoDiffIframe", model);
            }
            catch (Exception ex)
            {
                return PartialView("ErrorPartial", ex.Message);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetDiff(int prId, string filePath)
        {
            var prData = await _azureService.GetPRCompleteDataWithDiffsAsync(prId);

            if (!prData.Success || !prData.FileDiffs.ContainsKey(filePath))
            {
                return View("Error");
            }

            var fileDiff = prData.FileDiffs[filePath];

            // Obtener contenido original y modificado
            var originalContent = await _azureService.GetFileContentAtCommit(
                prData.PRDetails.Repository.Id,
                prData.PRDetails.LastMergeTargetCommit.CommitId,
                filePath
            );

            var modifiedContent = await _azureService.GetFileContentAtCommit(
                prData.PRDetails.Repository.Id,
                prData.PRDetails.LastMergeSourceCommit.CommitId,
                filePath
            );


            return Json(new { originalContent, modifiedContent });
        }

        // Agrega este método helper en tu controller
        private string JavaScriptEncode(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            return input
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\"", "\\\"")
                .Replace("\r\n", "\\n")
                .Replace("\n", "\\n")
                .Replace("\r", "\\n")
                .Replace("\t", "\\t");
        }

        private string GetLanguageFromExtension(string filePath)
        {
            return Path.GetExtension(filePath).ToLower() switch
            {
                ".cs" => "csharp",
                ".js" => "javascript",
                ".ts" => "typescript",
                ".html" => "html",
                ".css" => "css",
                ".sql" => "sql",
                ".json" => "json",
                ".xml" => "xml",
                ".py" => "python",
                ".java" => "java",
                ".php" => "php",
                ".rb" => "ruby",
                ".go" => "go",
                ".rs" => "rust",
                ".cpp" => "cpp",
                ".c" => "c",
                ".h" => "c",
                _ => "plaintext"
            };
        }

        // Nueva acción para redirigir a Monaco
        [HttpPost]
        public IActionResult OpenInMonaco(int prId, string filePath)
        {
            return RedirectToAction("MonacoDiff", new { prId, filePath });
        }

        [HttpGet]
        public async Task<IActionResult> Index(int prId = 570, string viewMode = "inline", string? filePath = null)
        {
            try
            {
                // Obtener datos del PR
                var prData = await _azureService.GetPRCompleteDataWithDiffsAsync(prId);

                if (!prData.Success)
                {
                    ViewBag.Error = prData.ErrorMessage;
                    return View();
                }

                var viewModel = new PRViewModel
                {
                    PRId = prId,
                    PRDetails = prData.PRDetails,
                    Files = prData.Files,
                    FileDiffs = prData.FileDiffs,
                    Summary = prData.Summary,
                    ViewMode = viewMode,
                    SelectedFilePath = filePath!
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                return View();
            }
        }

        [HttpGet]
        public async Task<IActionResult> FileDiff(int prId, string filePath, string viewMode = "inline")
        {
            try
            {
                var prData = await _azureService.GetPRCompleteDataWithDiffsAsync(prId);

                if (!prData.Success || !prData.FileDiffs.ContainsKey(filePath))
                {
                    return PartialView("_FileDiffError", new FileDiffPartialModel
                    {
                        ErrorMessage = "No se pudo cargar el diff del archivo"
                    });
                }

                var model = new FileDiffPartialModel
                {
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    DiffLines = prData.FileDiffs[filePath].DiffLines,
                    ViewMode = viewMode,
                    AddedLines = prData.FileDiffs[filePath].AddedLines,
                    DeletedLines = prData.FileDiffs[filePath].DeletedLines
                };

                return PartialView("_FileDiff", model);
            }
            catch (Exception ex)
            {
                return PartialView("_FileDiffError", new FileDiffPartialModel
                {
                    ErrorMessage = ex.Message
                });
            }
        }

        [HttpPost]
        public IActionResult ChangeViewMode(int prId, string viewMode, string filePath = null)
        {
            return RedirectToAction("Index", new { prId, viewMode, filePath });
        }

        [HttpGet]
        public async Task<IActionResult> EstadoPullRequest(int prId, int definitionId)
        {
            _azureService.ForceProyectType(AzureProjectType.GeneXus);
            var pr = await _azureService.GetPullRequestAsync(prId);
            var buildInfo = await _azureService.GetCompleteInfoPullRequestAndBuildFast(prId, definitionId);
            var model = _mapper.Map<PullRequestForceManualViewModel>(pr);
            model.Build = buildInfo;
            var buildGx = await _dbRepository.GetBuildByIdPullRequest(prId);
            if (buildGx != null)
            {
                model.Owner = buildGx.Usuario;
            }
            ViewBag.DefinitionId = definitionId;
            ViewBag.Gx = true;
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> EstadoPullRequestScripts(int prId, int definitionId)
        {
            var pr = await _azureService.GetPullRequestAsync(prId);
            var buildInfo = await _azureService.GetCompleteInfoPullRequestAndBuild(prId);
            var model = _mapper.Map<PullRequestForceManualViewModel>(pr);
            model.Build = buildInfo;

            var buildGx = await _dbRepository.GetScriptByPullRequest(prId);
            if (buildGx != null)
            {
                model.Owner = buildGx.Usuario;
            }
            ViewBag.DefinitionId = await _azureService.FindBuildDefinitionAsync(pr.Repository.Id, pr.Repository.Name, pr.TargetRefName);
            //ViewBag.DefinitionId = buildInfo.DefinitionId;
            ViewBag.Gx = false;
            return View("EstadoPullRequest", model);
        }

        [HttpPost]
        public async Task<IActionResult> ForceToManualBuild(ForceBuildManualViewModel request)
        {
            var aplicativos = _configuration.GetSection("Projects").Get<List<ProjectSetting>>()?.FirstOrDefault(x => x.Active && x.IsGx)?.Repos.Where(x => x.OwnBuild.HasValue && x.OwnBuild.Value);
            var bt = aplicativos!.FirstOrDefault(x => x.RepoName == request.Repository);
            if (bt != null)
            {
                _azureService.ForceProyectType(AzureProjectType.GeneXus);
                var buildID = await _azureService.TriggerBuildManual(bt.BuildDefinitionId ?? 0 , request.Owner, request.PullRequestId, _configuration["AzureSettings:BranchPR"]!);
                if (buildID != 0)
                    await _tempDataHelper.Message(TypeNotify.Success);
                else
                    await _tempDataHelper.Message(TypeNotify.Error, "No se pudo disparar el trigger manual del build", "Error al correr Build");
            }
            return Json(new { success = true, redirectUrl = ViewBag.ReturnUrl });
            //return Redirect(ViewBag.ReturnUrl);
        }

        [HttpPost]
        public async Task<IActionResult> ForceToManualBuildScript(ForceBuildManualViewModel request)
        {

            var buildID = await _azureService.TriggerBuildManual(request.DefinitionId, request.Owner, request.PullRequestId, _configuration["AzureSettings:BranchPR"]!);
            if (buildID != 0)
                await _tempDataHelper.Message(TypeNotify.Success);
            else
                await _tempDataHelper.Message(TypeNotify.Error, "No se pudo disparar el trigger manual del build", "Error al correr Build");

            return Json(new { success = true, redirectUrl = ViewBag.ReturnUrl });
            //return Redirect(ViewBag.ReturnUrl);
        }

        private string GetProjectDevopsByRepoId(string repoId)
        {
            var aplicativos = _configuration.GetSection("Projects").Get<List<ProjectSetting>>()?.FirstOrDefault(x => x.Active && x.IsGx)?.Repos.Where(x => x.OwnBuild.HasValue && x.OwnBuild.Value);
            var bt = aplicativos!.FirstOrDefault(x => x.RepoName == repoId);
            if (bt != null)
                return _configuration["AzureSettings:ProjectGx"]!;
            else
                return _configuration["AzureSettings:Project"]!;
        }

        [HttpPost]
        public async Task<IActionResult> RevertAndCreatePr(ForceBuildManualViewModel request)
        {
            // Verificar si se puede revertir
            _gitRevertService.ProjectDevops = GetProjectDevopsByRepoId(request.Repository);
            _gitRevertService.RepositoryId = request.Repository;

            if (!await _gitRevertService.CanRevertPullRequest(request.PullRequestId))
            {
                await _tempDataHelper.Message(TypeNotify.Error, "No se pudo disparar el trigger manual del build", "Error al correr Build");
            }
            // Ejecutar revert
            var result = await _gitRevertService.RevertMergeAndCreatePr(request.PullRequestId, RevertStrategy.GetStandardRevertStrategy());

            if (result.Success)
            {
                return Ok(new
                {
                    Message = "Revert completado exitosamente",
                    OriginalPullRequestId = request.PullRequestId,
                    NewPullRequestId = result.NewPullRequestId,
                    NewBranchName = result.NewBranchName,
                    RevertCommitId = result.NewCommitId

                });
            }
            else
            {
                return BadRequest(new
                {
                    Error = "Error en el proceso de revert",
                    Details = result.ErrorMessage
                });
            }

        }
        public async Task<IActionResult> RecreatePullRequest(ForceBuildManualViewModel request)
        {
            try
            {
                // Verificar si se puede revertir
                _gitRevertService.ProjectDevops = GetProjectDevopsByRepoId(request.Repository);
                _gitRevertService.RepositoryId = request.Repository;

                var result = await _gitRevertService.RecreatePullRequestManual(request.PullRequestId);

                if (result.Success)
                {
                    var build = await _dbRepository.GetBuildByIdPullRequest(request.PullRequestId);
                    if (build != null)
                    {
                        build.IdPr = result.NewPullRequestId;
                        build.BranchName = result.NewBranchName;
                        await _dbRepository.UpdatePasaje(build);
                        await _tempDataHelper.Message(TypeNotify.Success, $"PR recreado exitosamente. Nuevo PullRequestId: {result.NewPullRequestId}", "Cambios");
                    }
                    //return RedirectToAction("EstadoPullRequest", new { prId = result.NewPullRequestId, definitionId = request.DefinitionId });
                    return Json(new { success = true, redirectUrl = Url.Action("EstadoPullRequest", "PullRequest", new { prId = result.NewPullRequestId, definitionId = request.DefinitionId }) });
                }
                else
                {
                    //return BadRequest(new { Error = result.ErrorMessage });
                    return Json(new
                    {
                        success = false,
                        message = "No se pudo completar la acción.",
                        errors = result.ErrorMessage ?? ""
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recreando PR {PullRequestId}", request.PullRequestId);
                return Json(new
                {
                    success = false,
                    message = "No se pudo completar la acción.",
                    errors = "Error interno en el servidor."
                });
            }
        }
        [HttpGet]
        public async Task<ActionResult<QuickStatusResult>> GetQuickStatus(int prId, string repositoryId)
        {
            try
            {
                var repos = _configuration.GetSection("Projects").Get<List<ProjectSetting>>()?.FirstOrDefault(x => x.Active)?.Repos;
                var repo = repos.FirstOrDefault(x => x.RepoName == repositoryId);
                if (repo == null) return NotFound();

                _azureService.ForceProyectType(AzureProjectType.GeneXus);

                // Obtener solo la información básica (más rápido)
                var pr = await _azureService.GetPullRequestAsync(prId);
                var build = await _azureService.GetBuildByPullRequestAsync(prId, repo.BuildDefinitionId);

                return Json(new QuickStatusResult
                {
                    PullRequestStatus = pr?.Status ?? "unknown",
                    PRStatusBadgeClass = GetPrBadgeClass(pr?.Status!),
                    Status = build?.Status!,
                    Result = build?.Result!,
                    BuildStatusBadgeClass = GetBuildBadgeClass(build?.Status!, build?.Result!)
                });
            }
            catch
            {
                return Json(new QuickStatusResult
                {
                    PullRequestStatus = "error",
                    PRStatusBadgeClass = "badge badge-danger"
                });
            }
        }

        private string GetPrBadgeClass(string status)
        {
            return status?.ToLower() switch
            {
                "active" => "badge badge-primary",
                "completed" => "badge badge-success",
                "abandoned" => "badge badge-danger",
                _ => "badge badge-secondary"
            };
        }

        private string GetBuildBadgeClass(string status, string result)
        {
            if (status?.ToLower() == "inprogress")
                return "badge badge-primary";

            return result?.ToLower() switch
            {
                "succeeded" => "badge badge-success",
                "failed" => "badge badge-danger",
                "canceled" => "badge badge-warning",
                _ => "badge badge-info"
            };
        }

        public class QuickStatusResult
        {
            public string PullRequestStatus { get; set; } = "";
            public string PRStatusBadgeClass { get; set; } = "";
            public string Status { get; set; } = "";
            public string Result { get; set; } = "";
            public string BuildStatusBadgeClass { get; set; } = "";
        }


    }
}
