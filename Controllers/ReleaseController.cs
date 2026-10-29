using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PasadorScript.DTO;
using PasadorScript.DTO.AzureDev;
using PasadorScript.Extensions;
using PasadorScript.Helpers;
using PasadorScript.Models.Reports;
using PasadorScript.Repository;
using PasadorScript.Services.AdGroups;
using PasadorScript.Services.Azure;
using PasadorScript.Services.Graph;
using PasadorScript.Services.Release;
using PasadorScript.Validators;
using PasadorScript.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Net.Http;
using System.Text;
using X.PagedList;
using X.PagedList.Extensions;

namespace PasadorScript.Controllers
{
    public class ReleaseController : Controller
    {
        private readonly ILogger<ReleaseController> _logger;
        private readonly IConfiguration _configuration;
        private readonly TempDataHelper _tempDataHelper;
        private readonly IAdGroupService _adGroupService;
        private readonly IServiciosGraph _serviciosGraph;
        private readonly IAzureService _azureService;
        private readonly IDbRepository _dbRepository;
        private readonly IResolucionAprobacionRelease _resolucionAprobacion;

        public ReleaseController(ILogger<ReleaseController> logger,
            IConfiguration configuration, IAzureService azureService,
            TempDataHelper tempDataHelper, IAdGroupService adGroupService, IServiciosGraph serviciosGraph,
            IDbRepository dbRepository, IResolucionAprobacionRelease resolucionAprobacion)
        {
            _logger = logger;
            _configuration = configuration;
            _tempDataHelper = tempDataHelper;
            _adGroupService = adGroupService;
            _serviciosGraph = serviciosGraph;
            _azureService = azureService;
            _dbRepository = dbRepository;
            _resolucionAprobacion = resolucionAprobacion;
        }
        public async Task<IActionResult> Auditoria(int releaseId, int pullRequestId, string projectType = "Genexus", string? returnUrl = null)
        {
            try
            {
                _azureService.ForceProyectType(projectType.ToAzureProjectType());
                var audit = await _azureService.ObtenerAuditoriaReleaseAsync(releaseId, pullRequestId);
                ViewBag.ReturnUrl = returnUrl ?? Url.Action("ViewStatus", new { id = pullRequestId, projectType });
                return View("AuditView", audit);

            }
            catch (Exception ex)
            {
                await _tempDataHelper.Message(TypeNotify.Error, $"Error al obtener auditoría: {ex.Message}", $"Error");
                return RedirectToAction("ViewStatus", new { id = pullRequestId, projectType });
            }
        }

        [Authorize]
        [RequireValidPat]
        public async Task<IActionResult> ViewStatus(int id, string projectType = "Genexus", string? returnUrl = null)
        {
            ReleaseDto? release = null;
            AzureDevopsPullRequest? prObj = null;
            _azureService.ForceProyectType(projectType.ToAzureProjectType());
            release = await _azureService.ObtenerReleaseDesdePullRequestAsync(id);
            prObj = await _azureService.GetPullRequestAsync(id);

            if (release == null)
            {
                return View("NotFound");
            }
            release.PullRequestObject = prObj;
            release.PullRequest = id;
            release.AzureProjectType = projectType;
            release.ForGenexus = projectType == "Genexus" || projectType == "GenexusOld";
            await CompletarVistaReleaseAsync(release);
            return View(release);
        }

        [Authorize]
        [RequireValidPat]
        public async Task<IActionResult> ViewStatusByBuild(string projectName, int buildId, bool serviciosGx, string? returnUrl = null)
        {
            ReleaseDto? release = null;
            AzureDevopsPullRequest? prObj = null;

            var prId = await _azureService.GetPrIdByBuildId(projectName, buildId);
            prObj = await _azureService.GetPullRequestAsync(projectName, prId ?? 0);
            if (serviciosGx)
            {
                _azureService.ForceProyectType(AzureProjectType.GeneXus);
            }
            release = await _azureService.ObtenerReleaseDesdePullRequestAsync(projectName, prId ?? 0);

            if (release == null)
            {
                return View("NotFound");
            }
            release.PullRequestObject = prObj;
            release.PullRequest = prId ?? 0;
            release.ForGenexus = serviciosGx;
            await CompletarVistaReleaseAsync(release);
            return View("ViewStatus", release);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> ActualizarRelease(int releaseId, int environmentId, ReleaseEnvironmentAction action, string comentario, int pullRequestId, bool forGenexus = true)
        {
            var log = await _adGroupService.GetInfoUser();
            ReleaseEnvironmentActionResult? r = null;
            if (forGenexus)
            {
                _azureService.ForceProyectType(AzureProjectType.GeneXus);
            }
            var pr = await _azureService.GetPullRequestAsync(pullRequestId);
            if (pr?.CreatedBy.UniqueName == log.EmailAzure)
            {
                await _tempDataHelper.Message(TypeNotify.Error, "No puede aprobar su propio despliegue", "Error con status code 401");
                return RedirectToAction("ViewStatus", new { id = pullRequestId, serviciosGx = forGenexus });
            }
            r = await _azureService.ActualizarEstadoEnvironmentAsync(releaseId, environmentId, action, comentario);


            if (r != null)
            {
                if (r.Success)
                    await _tempDataHelper.Message(TypeNotify.Success, title: $"Release {releaseId}");
                else
                    await _tempDataHelper.Message(TypeNotify.Error, r.ErrorMessage!, $"Error con status code {r.StatusCode}");
            }

            return RedirectToAction("ViewStatus", new { id = pullRequestId, serviciosGx = forGenexus });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ActualizarApproval(
            int releaseId,
            int approvalId,
            ReleaseEnvironmentAction action,
            string? comentario,
            int pullRequestId,
            string projectType,
            string? sinPrueba)
        {
            IActionResult Volver() =>
                RedirectToAction("ViewStatus", new { id = pullRequestId, projectType });

            if (releaseId <= 0 || approvalId <= 0 || pullRequestId <= 0)
            {
                await _tempDataHelper.Message(TypeNotify.Error, "No se pudo actualizar la aprobación.", "Error");
                return Volver();
            }

            var usuario = await _adGroupService.GetInfoUser();
            _azureService.ForceProyectType(projectType.ToAzureProjectType());

            var release = await _azureService.ObtenerReleaseDesdePullRequestAsync(pullRequestId);
            if (release == null)
            {
                await _tempDataHelper.Message(TypeNotify.Error, "No se encontró el release.", "Error");
                return Volver();
            }

            var permitida = _resolucionAprobacion.BuscarPermitida(release, RolesDe(usuario), approvalId);
            if (permitida == null)
            {
                await _tempDataHelper.Message(TypeNotify.Error, "Su rol no puede realizar esta acción sobre la etapa.", "Error");
                return Volver();
            }

            // El grupo lo define la aprobación resuelta. El descriptor del formulario no alcanza para autorizar.
            var pertenece = await _serviciosGraph.UsuarioPerteneceAGrupoAsync(usuario.Username, permitida.Descriptor);
            if (!pertenece)
            {
                await _tempDataHelper.Message(TypeNotify.Error, "Su usuario no pertenece al grupo seleccionado", "Error con status code 401");
                return Volver();
            }

            var pr = await _azureService.GetPullRequestAsync(pullRequestId);
            if (pr?.CreatedBy.UniqueName == usuario.EmailAzure)
            {
                await _tempDataHelper.Message(TypeNotify.Error, "No puede aprobar su propio despliegue", "Error con status code 401");
                return Volver();
            }

            // La previa de QA solo confirma. Validación Acotada aplica a la posterior.
            var sinPruebaBit = permitida.PermiteSinPrueba ? ParseSinPrueba(sinPrueba) : null;

            if (!TryArmarComentario(action, sinPruebaBit, comentario, out var comentarioAzure))
            {
                var aviso = action == ReleaseEnvironmentAction.Reject
                    ? "El motivo del rechazo es obligatorio."
                    : "Si marcás Validación Acotada, el comentario es obligatorio.";
                await _tempDataHelper.Message(TypeNotify.Error, aviso, "Error");
                return Volver();
            }

            var resultado = await _azureService.ActualizarEstadoEnvironmentAsync(approvalId, action, comentarioAzure);
            if (resultado is not { Success: true })
            {
                await _tempDataHelper.Message(
                    TypeNotify.Error,
                    resultado?.ErrorMessage ?? "No se pudo actualizar la aprobación.",
                    $"Error con status code {resultado?.StatusCode}");
                return Volver();
            }

            if (action == ReleaseEnvironmentAction.Approve && sinPruebaBit.HasValue)
            {
                var guardado = await _dbRepository.GuardarSinPruebaPorPullRequestAsync(pullRequestId, sinPruebaBit.Value);
                if (!guardado)
                {
                    _logger.LogError(
                        "Aprobación {ApprovalId} aplicada en Azure para el pull request {PullRequestId} sin envío local para Sin Prueba",
                        approvalId,
                        pullRequestId);
                    await _tempDataHelper.Message(
                        TypeNotify.Error,
                        "La aprobación se aplicó en Azure. No se pudo guardar Sin Prueba porque no hay un envío local para este pull request.",
                        "Error");
                    return Volver();
                }
            }

            await _tempDataHelper.Message(TypeNotify.Success, title: $"Release {releaseId}");
            return Volver();
        }

        private async Task CompletarVistaReleaseAsync(ReleaseDto release)
        {
            release.IsHotfix = await _dbRepository.GetIsHotfixByPullRequestAsync(release.PullRequest);
            var usuario = await _adGroupService.GetInfoUser();
            var stages = _resolucionAprobacion.Resolver(release, RolesDe(usuario));
            ViewData[IResolucionAprobacionRelease.ViewDataKey] = stages.ToDictionary(stage => stage.EnvironmentId);
        }

        private static string[] RolesDe(AuthUserAd usuario) =>
            usuario.Roles
                .Select(rol => rol.GroupName)
                .Where(nombre => !string.IsNullOrWhiteSpace(nombre))
                .ToArray()!;

        private static bool? ParseSinPrueba(string? valor) => valor switch
        {
            "1" => true,
            "0" => false,
            _ => null
        };

        private static bool TryArmarComentario(
            ReleaseEnvironmentAction action,
            bool? sinPrueba,
            string? comentario,
            out string comentarioAzure)
        {
            var texto = comentario?.Trim() ?? "";
            if (action == ReleaseEnvironmentAction.Reject)
            {
                comentarioAzure = texto;
                return texto.Length > 0;
            }

            if (sinPrueba == true && texto.Length == 0)
            {
                comentarioAzure = "";
                return false;
            }

            comentarioAzure = sinPrueba.HasValue && texto.Length > 0
                ? texto
                : IResolucionAprobacionRelease.ComentarioAprobadoManualmente;
            return true;
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CancelarRelease(
            int releaseId,
            int environmentId,
            int pullRequestId,
            string projectType,
            string? comentario)
        {
            if (releaseId <= 0 || environmentId <= 0)
            {
                await _tempDataHelper.Message(TypeNotify.Error, title: $"Release {releaseId}", notifyMessage: "No se pudo cancelar la release");
                return RedirectToAction("ViewStatus", new { id = pullRequestId, projectType });
            }

            _azureService.ForceProyectType(projectType.ToAzureProjectType());

            var res = await _azureService.CancelarEnvironmentAsync(
                releaseId, environmentId, comentario ?? "Cancelado desde PasadorScript");

            if (res)
                await _tempDataHelper.Message(TypeNotify.Success, title: $"Release {releaseId}");
            else
                await _tempDataHelper.Message(TypeNotify.Error, title: $"Release {releaseId}", notifyMessage: "No se pudo cancelar la release");

            return RedirectToAction("ViewStatus", new
            {
                id = pullRequestId,
                projectType = projectType,
            });
        }

        [HttpGet]
        public async Task<ActionResult<string>> GetLogRelease(int idRelease, bool fromGenexus = true)
        {
            string? logs = null;
            if (fromGenexus)
            {
                _azureService.ForceProyectType(AzureProjectType.GeneXus);
                logs = await _azureService.DownloadReleaseLogsAsync(idRelease);
            }
            else
                logs = await _azureService.DownloadReleaseLogsAsync(idRelease, _configuration["AzureSettings:TaskEjecucionScriptName"] ?? "");
            return Json(new { log = logs ?? $"No se ha encontrado log para la release {idRelease}" });
        }

        [HttpPost]
        public async Task<ActionResult> ReDeploy(int releaseId, int environmentId, int pullRequestId, string? comment, string projectType)
        {
            bool b;
            _azureService.ForceProyectType(projectType.ToAzureProjectType());
            b = await _azureService.RedeployEnvironmentAsync(releaseId, environmentId, comment);

            if (b)
                await _tempDataHelper.Message(TypeNotify.Success, "Redeploy iniciado...");
            else
                await _tempDataHelper.Message(TypeNotify.Error, "No se ha podido re deployar el release seleccionado", "Error con status code 400");

            return RedirectToAction("ViewStatus", new { id = pullRequestId, projectType = projectType });
        }

        [HttpPost]
        public async Task<ActionResult> Deploy(int releaseId, int environmentId, int pullRequestId, string? comment, string projectType)
        {
            bool b;
            _azureService.ForceProyectType(projectType.ToAzureProjectType());
            b = await _azureService.RedeployEnvironmentAsync(releaseId, environmentId, comment);

            if (b)
                await _tempDataHelper.Message(TypeNotify.Success, "Deploy iniciado...");
            else
                await _tempDataHelper.Message(TypeNotify.Error, "No se ha podido deployar el release seleccionado", "Error con status code 400");

            return RedirectToAction("ViewStatus", new { id = pullRequestId, projectType = projectType });
        }

        [HttpGet]
        public async Task<ActionResult> GetReleaseTasksAndLogs(int idRelease, int environmentId, string projectType)
        {
            try
            {
                ReleaseTasksAndLogsDto? result = null;

                _azureService.ForceProyectType(projectType.ToAzureProjectType());
                result = await _azureService.GetReleaseTasksAndLogsAsync(idRelease, environmentId);

                if (result == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"No se encontraron tareas para la release {idRelease} en el environment {environmentId}"
                    });
                }

                return Json(new
                {
                    success = true,
                    data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo tareas y logs de release {ReleaseId}", idRelease);
                return Json(new
                {
                    success = false,
                    message = $"Error al obtener los logs: {ex.Message}"
                });
            }
        }

        [HttpGet]
        public async Task<ActionResult> GetTaskLog(int idRelease, int environmentId, int taskId, string projectType)
        {
            try
            {
                string? log = null;

                _azureService.ForceProyectType(projectType.ToAzureProjectType());
                log = await _azureService.GetReleaseTaskLogAsync(idRelease, environmentId, taskId);

                return Json(new
                {
                    success = true,
                    log = log ?? $"No se encontró log para la tarea {taskId}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo log de tarea {TaskId}", taskId);
                return Json(new
                {
                    success = false,
                    message = $"Error al obtener el log de la tarea: {ex.Message}"
                });
            }
        }
        [HttpGet]
        public async Task<ActionResult> GetReleaseStatus(int id, string projectType)
        {
            try
            {
                ReleaseDto? release = null;
                AzureDevopsPullRequest? prObj = null;
                _azureService.ForceProyectType(projectType.ToAzureProjectType());
                release = await _azureService.ObtenerReleaseDesdePullRequestAsync(id);
                prObj = await _azureService.GetPullRequestAsync(id);

                if (release == null)
                {
                    return Json(new { success = false, message = "Release no encontrada" });
                }

                release.PullRequestObject = prObj;
                release.PullRequest = id;
                release.AzureProjectType = projectType;
                release.ForGenexus = projectType == "Genexus" || projectType == "GenexusOld";

                // Preparar datos para la vista
                var environments = release.Environments.Select(e => new
                {
                    Id = e.Id,
                    Name = e.Name,
                    Status = e.Status,
                    StatusBadgeLabel = e.StatusBadgeLabel,
                    StatusLabel = e.StatusLabel,
                    ModifiedOn = e.ModifiedOn,
                    Attempt = e.Attempt,
                    PreDeployApprovals = e.PreDeployApprovals,
                    PostDeployApprovals = e.PostDeployApprovals,
                    PreApprovedDefinition = e.PreApprovedDefinition,
                    PostApprovedDefinition = e.PostApprovedDefinition
                }).ToList();

                return Json(new
                {
                    success = true,
                    releaseId = release.Id,
                    releaseName = release.Name,
                    environments,
                    hasPendingApprovals = environments.Any(e =>
                        e.Status == "pending" ||
                        e.Status == "inProgress" ||
                        (e.Status == "inProgress" && e.PostDeployApprovals.Any(a => a.Status == "pending"))
                    )
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo estado de release {ReleaseId}", id);
                return Json(new
                {
                    success = false,
                    message = $"Error al obtener el estado: {ex.Message}"
                });
            }
        }

        [HttpGet]
        public async Task<ActionResult> GetEnvironmentStatus(int releaseId, int environmentId, bool serviciosGx = true)
        {
            try
            {
                if (serviciosGx)
                {
                    _azureService.ForceProyectType(AzureProjectType.GeneXus);
                }

                // Obtener información específica del environment
                var release = await _azureService.ObtenerReleaseDesdePullRequestAsync(releaseId);
                if (release == null)
                {
                    return Json(new { success = false, message = "Release no encontrada" });
                }

                var environment = release.Environments.FirstOrDefault(e => e.Id == environmentId);
                if (environment == null)
                {
                    return Json(new { success = false, message = "Environment no encontrado" });
                }

                var prePendingApprovals = environment.PreDeployApprovals
                    .Where(a => a.Status == "pending")
                    .Select(a => new { a.Id, a.Approver.DisplayName, a.Approver.Descriptor, IsPost = false })
                    .ToList();

                var postPendingApprovals = environment.PostDeployApprovals
                    .Where(a => a.Status == "pending")
                    .Select(a => new { a.Id, a.Approver.DisplayName, a.Approver.Descriptor, IsPost = true })
                    .ToList();

                var allPendingApprovals = prePendingApprovals.Concat(postPendingApprovals).ToList();

                return Json(new
                {
                    success = true,
                    environmentId = environment.Id,
                    status = environment.Status,
                    statusBadgeLabel = environment.StatusBadgeLabel,
                    statusLabel = environment.StatusLabel,
                    modifiedOn = environment.ModifiedOn,
                    attempt = environment.Attempt,
                    hasPendingApprovals = allPendingApprovals.Count > 0,
                    pendingApprovalCount = allPendingApprovals.Count,
                    prePendingApprovals,
                    postPendingApprovals,
                    allPendingApprovals
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo estado de environment {EnvironmentId}", environmentId);
                return Json(new { success = false, message = $"Error al obtener el estado: {ex.Message}" });
            }
        }
        // Reportes
        public async Task<IActionResult> ReleasesReport(string projectName, int definitionId, string stage, DateTime? fechaDesde, DateTime? fechaHasta, int? page, int pageSize = 20)
        {
            var projects = _configuration.GetSection("Projects").Get<List<ProjectSetting>>()!;
            ViewBag.Projects = new SelectList(projects, "ProjectName", "DisplayName");


            var viewModel = new ReleasesFilterViewModel
            {
                Projects = projects,
                SelectedProjectName = projectName,
                SelectedDefinitionId = definitionId,
                SelectedStage = stage,
                FechaDesde = fechaDesde,
                FechaHasta = fechaHasta,
                PageSize = pageSize,
                Releases = new StaticPagedList<ReleaseViewModel>(new List<ReleaseViewModel>(), 1, pageSize, 0)
            };

            // Proyecto seleccionado o primero
            var selectedProject = projects.FirstOrDefault(p => p.ProjectName == projectName);
            if (selectedProject == null && projects.Any())
            {
                selectedProject = projects.FirstOrDefault();
                viewModel.SelectedProjectName = selectedProject!.ProjectName;

            }
            if (selectedProject == null)
            {
                ViewBag.Error = "No hay proyectos configurados.";
                return View(viewModel);
            }
            viewModel.SelectedIsGx = selectedProject.IsGx;
            viewModel.CurrentRepos = selectedProject.Repos;

            // Definición seleccionada o primera
            if (definitionId == 0 && viewModel.CurrentRepos.Any())
            {
                viewModel.SelectedDefinitionId = viewModel.CurrentRepos.First().ReleaseDefinitionId;
            }


            try
            {
                int pageNumber = page ?? 1;

                if (viewModel.SelectedDefinitionId != 0)
                {
                    viewModel.Stages = await _azureService.GetDefinitionEnvironments(selectedProject.ProjectName, viewModel.SelectedDefinitionId);
                }

                // Convertir fechas a UTC para la consulta (día completo)
                DateTime? desdeUtc = null;
                if (fechaDesde.HasValue)
                {
                    desdeUtc = new DateTime(fechaDesde.Value.Year, fechaDesde.Value.Month, fechaDesde.Value.Day, 0, 0, 0, DateTimeKind.Utc);
                }
                DateTime? hastaUtc = null;
                if (fechaHasta.HasValue)
                {
                    hastaUtc = new DateTime(fechaHasta.Value.Year, fechaHasta.Value.Month, fechaHasta.Value.Day, 23, 59, 59, DateTimeKind.Utc);
                }
                var allReleases = await _azureService.GetReleasesReport(selectedProject.ProjectName, viewModel.SelectedDefinitionId, desdeUtc, hastaUtc, stage);
                viewModel.Releases = allReleases.ToPagedList(pageNumber, pageSize);
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Error al consultar la API: {ex.Message}";
            }

            return View(viewModel);
        }

        [HttpGet("GetDefinitionsAndStages")]
        public IActionResult GetDefinitionsAndStages(string projectName)
        {
            var projects = _configuration.GetSection("Projects").Get<List<ProjectSetting>>()!;
            var project = projects.FirstOrDefault(p => p.ProjectName == projectName);
            if (project == null) return Json(new { definitions = new List<object>() });

            var definitions = project.Repos.Select(d => new
            {
                definitionId = d.ReleaseDefinitionId,
                pipelineName = d.PipelineName
            }).ToList();

            return Json(new { definitions });
        }

        [HttpGet]
        public async Task<IActionResult> GetStages(string projectName, int definitionId)
        {
            var stages = await _azureService.GetDefinitionEnvironments(projectName, definitionId);
            return Json(stages);
        }

        [HttpGet]
        public async Task<IActionResult> ExportToExcel(string projectName, int definitionId, string stage, DateTime? fechaDesde, DateTime? fechaHasta)
        {
            // Convertir fechas a UTC igual que en Index
            DateTime? desdeUtc = fechaDesde.HasValue ? new DateTime(fechaDesde.Value.Year, fechaDesde.Value.Month, fechaDesde.Value.Day, 0, 0, 0, DateTimeKind.Utc) : null;
            DateTime? hastaUtc = fechaHasta.HasValue ? new DateTime(fechaHasta.Value.Year, fechaHasta.Value.Month, fechaHasta.Value.Day, 23, 59, 59, DateTimeKind.Utc) : null;

            var releases = await _azureService.GetReleasesReport(projectName, definitionId, desdeUtc, hastaUtc, stage);

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Releases");

                // Encabezados
                worksheet.Cell(1, 1).Value = "Release";
                worksheet.Cell(1, 2).Value = "Stage";
                worksheet.Cell(1, 3).Value = "Release ID";
                worksheet.Cell(1, 4).Value = "Build";
                worksheet.Cell(1, 5).Value = "Branch";
                worksheet.Cell(1, 6).Value = "Deployed by";
                worksheet.Cell(1, 7).Value = "Queued on";

                // Estilo de encabezado
                var headerRow = worksheet.Row(1);
                headerRow.Style.Font.Bold = true;
                headerRow.Style.Fill.BackgroundColor = XLColor.LightGray;

                int row = 2;
                foreach (var item in releases)
                {
                    worksheet.Cell(row, 1).Value = item.ReleaseDefinition;
                    worksheet.Cell(row, 2).Value = item.Stage;
                    worksheet.Cell(row, 3).Value = item.ReleaseId;
                    worksheet.Cell(row, 4).Value = item.Build;
                    worksheet.Cell(row, 5).Value = item.Branch;
                    worksheet.Cell(row, 6).Value = item.DeployedBy;
                    worksheet.Cell(row, 7).Value = item.QueuedOn.ToString("dd/MM/yyyy, HH:mm");
                    row++;
                }

                // Ajustar ancho de columnas automáticamente
                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Releases_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
                }
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportToPdf(string projectName, int definitionId, string stage, DateTime? fechaDesde, DateTime? fechaHasta)
        {
            // Convertir fechas a UTC
            DateTime? desdeUtc = fechaDesde.HasValue ? new DateTime(fechaDesde.Value.Year, fechaDesde.Value.Month, fechaDesde.Value.Day, 0, 0, 0, DateTimeKind.Utc) : null;
            DateTime? hastaUtc = fechaHasta.HasValue ? new DateTime(fechaHasta.Value.Year, fechaHasta.Value.Month, fechaHasta.Value.Day, 23, 59, 59, DateTimeKind.Utc) : null;

            var releases = await _azureService.GetReleasesReport(projectName, definitionId, desdeUtc, hastaUtc, stage);

            // Generar PDF con QuestPDF
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.Header().Text("Seguimiento de despliegues en Azure DevOps").SemiBold().FontSize(20);
                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Release").Bold();
                            header.Cell().Text("Stage").Bold();
                            header.Cell().Text("Release ID").Bold();
                            header.Cell().Text("Build").Bold();
                            header.Cell().Text("Branch").Bold();
                            header.Cell().Text("Deployed by").Bold();
                            header.Cell().Text("Queued on").Bold();
                        });

                        foreach (var item in releases)
                        {
                            table.Cell().Text(item.ReleaseDefinition);
                            table.Cell().Text(item.Stage);
                            table.Cell().Text(item.ReleaseId.ToString());
                            table.Cell().Text(item.Build);
                            table.Cell().Text(item.Branch);
                            table.Cell().Text(item.DeployedBy);
                            table.Cell().Text(item.QueuedOn.ToString("dd/MM/yyyy, HH:mm"));
                        }
                    });
                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("Página ");
                        text.CurrentPageNumber();
                    });
                });
            });

            var pdfBytes = document.GeneratePdf();
            return File(pdfBytes, "application/pdf", $"Releases_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
        }
    }
}
