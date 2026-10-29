using Azure.Core;
using DocumentFormat.OpenXml.Office2016.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.Extensions.Configuration;
using PasadorScript.DbModels;
using PasadorScript.DTO.AzureDev;
using PasadorScript.DTO.TeamDev;
using PasadorScript.Extensions;
using PasadorScript.Helpers;
using PasadorScript.Repository;
using PasadorScript.Services.AdGroups;
using PasadorScript.Services.Azure;
using PasadorScript.Services.Email;
using PasadorScript.Services.Environments;
using PasadorScript.Services.Pat;
using PasadorScript.Validators;
using PasadorScript.ViewModels;
using System.Collections.Generic;
using System.Text.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace PasadorScript.Controllers
{
    [Route("GxDeploy")]
    [Authorize]
    [RequireValidPat]
    public class GxDeployController : Controller
    {
        private readonly IEmailService _emailService;
        private readonly NuevoP229Context _dbContext;
        private readonly IDbRepository _dbRepository;
        private readonly IAdGroupService _adGroupService;
        private readonly TempDataHelper _tempDataHelper;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GxDeployController> _logger;
        private readonly IModelMetadataProvider _metadataProvider;
        private readonly IPatRepositoryService _patRepositoryService;
        private readonly IAzureService _azureService;
        private readonly IEnvironmentService _environmentService;

        public GxDeployController(ILogger<GxDeployController> logger, IDbRepository dbRepository, IAdGroupService adGroupService, TempDataHelper tempDataHelper,
                                  IConfiguration configuration, IModelMetadataProvider metadataProvider,
                                  IPatRepositoryService patRepositoryService, IAzureService azureService, NuevoP229Context dbContext,
            IEmailService emailService, IEnvironmentService environmentService)
        {
            _emailService = emailService;
            _dbContext = dbContext;
            _dbRepository = dbRepository;
            _adGroupService = adGroupService;
            _tempDataHelper = tempDataHelper;
            _configuration = configuration;
            _logger = logger;
            _metadataProvider = metadataProvider;
            _patRepositoryService = patRepositoryService;
            _azureService = azureService;
            _environmentService = environmentService;
        }

        [HttpGet("RedirectToAudit")]
        public async Task<IActionResult> RedirectToAudit(int idBuildGx)
        {
            try
            {
                
                // Obtener el script por ID
                var buildGx = await _dbRepository.BuildGxToViewModel(idBuildGx);

                if (buildGx == null || !buildGx.IdPr.HasValue)
                {
                    await _tempDataHelper.Message(TypeNotify.Error, "No se encontró el script o no tiene Pull Request asociado", "Error");
                    return RedirectToAction("Index");
                }

                var prId = buildGx.IdPr.Value;

                _azureService.ForceProyectType(buildGx.AzureProjectType.ToAzureProjectType());

                // Obtener la release desde el Pull Request (para scripts no GeneXus)
                var release = await _azureService.ObtenerReleaseDesdePullRequestAsync(buildGx.IdPr.Value);

                if (release != null)
                {
                    return RedirectToAction("Auditoria", "Release", new
                    {
                        releaseId = release.Id,
                        pullRequestId = prId,
                        proyectType = buildGx.AzureProjectType,
                        returnUrl = Url.Action("Index", "GxDeploy")
                    });
                }

                // Si NO hay release: obtener la info completa del PR (commits, reviewers, votos, comentarios)
                var prAudit = await _azureService.ObtenerPullRequestAuditInfoAsync(prId);

                var audit = new PasadorScript.DTO.Auditoria.ReleaseAuditDto
                {
                    ReleaseId = 0,
                    ReleaseName = $"PullRequest #{prId}",
                    CreatedOn = prAudit.CreationDate,
                    PullRequest = prAudit,
                    Environments = new List<PasadorScript.DTO.Auditoria.EnvironmentAuditDto>()
                };

                ViewBag.ReturnUrl = Url.Action("Index", "GxDeploy");
                return View("~/Views/Release/AuditView.cshtml", audit);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error redirigiendo a auditoría para buildGx {idBuildGx}", idBuildGx);
                await _tempDataHelper.Message(TypeNotify.Error, "Error al cargar el historial", "Error");
                return RedirectToAction("Index");
            }
        }


        private static string? RecortarObjetoGx(string? objetoGx)
        {
            if (string.IsNullOrWhiteSpace(objetoGx))
                return null;

            var recortado = objetoGx.Trim();
            return recortado.Length <= 100 ? recortado : recortado[..100];
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? usuario, string? descripcion, int page = 1, string? repositoryId = null, bool deleted = false, int? nroIncidente = null, string? objetoGx = null)
        {
            usuario = await UsuarioFiltroHelper.ResolverAsync(usuario, Request.Query, _adGroupService);
            objetoGx = RecortarObjetoGx(objetoGx);
            var apps = _configuration.GetSection("Projects").Get<List<ProjectSetting>>().Where(x => x.IsGx && x.Active).FirstOrDefault();
            var aplicativos = apps.Repos.Where(x=>x.OwnBuild.HasValue && x.OwnBuild.Value);
            ViewBag.Aplicativos = new SelectList(aplicativos, "RepoName", "DisplayName");
            RepoSetting? repo = null;
            if (repositoryId == null)
            {
                repo = aplicativos?.First()!;
            }
            else
            {
                repo = aplicativos?.First(x => x.RepoName == repositoryId)!;
            }
            var model = await _dbRepository.GetBuildsGx(deleted, repositoryId: repo.RepoName, username: usuario, descripcion, page, nroIncidente: nroIncidente, objetoGx: objetoGx);
            model.Environments = await _environmentService.GetEnvironmentDtosGxAsync(repositoryId);            
            model.RepositoryId = repo.RepoName;
            model.Usuario = usuario;
            model.Descripcion = descripcion;
            model.NroIncidente = nroIncidente;
            model.ObjetoGx = objetoGx;
            model.SoloPendientes = false;
            model.Deleted = deleted;

            return View(model);
        }

        [HttpGet("Pendientes")]
        public async Task<IActionResult> Pendientes(string? usuario, string? descripcion, int page = 1, string? repositoryId = null, int? nroIncidente = null, string? objetoGx = null)
        {
            usuario = await UsuarioFiltroHelper.ResolverAsync(usuario, Request.Query, _adGroupService);
            objetoGx = RecortarObjetoGx(objetoGx);
            var aplicativos = _configuration.GetProjectSettings(true, null, true, repositoryId);
            ViewBag.Aplicativos = new SelectList(aplicativos.Where(x => x.Active).FirstOrDefault().Repos.Where(x=>x.OwnBuild.HasValue && x.OwnBuild.Value), "RepoName", "DisplayName");
            RepoSetting? repo = null;
            RepoSetting? repoOld = null;
            if (string.IsNullOrEmpty(repositoryId))
            {
                repo = aplicativos.Where(x => x.Active).FirstOrDefault()!.Repos!.FirstOrDefault();
                repoOld = aplicativos.Where(x => !x.Active).FirstOrDefault()!.Repos!.FirstOrDefault();
            }
            else
            {
                repo = aplicativos.Where(x => x.Active).FirstOrDefault()!.Repos!.First(x => x.RepoName == repositoryId);
                repoOld = aplicativos.Where(x => !x.Active).FirstOrDefault()!.Repos!.FirstOrDefault(x => x.RepoName == repositoryId);
            }
            _azureService.ForceProyectType(AzureProjectType.GeneXus);

            List<int>? prActives = null;
            var reales = await _azureService.GetPendingReleasesWithPullRequestInfo(repo.ReleaseDefinitionId, "queued,pending,inProgress");
            prActives = reales.Select(x => x.PullRequestId ?? 0).ToList();
            _azureService.ForceProyectType(AzureProjectType.GenexusOld);
            var realesOld = await _azureService.GetPendingReleasesWithPullRequestInfo(repoOld.ReleaseDefinitionId, "queued,pending,inProgress");
            foreach (var r in realesOld.Select(x => x.PullRequestId ?? 0).ToList())
            {
                prActives.Add(r);
            }

            var model = await _dbRepository.GetBuildsGx(false, repositoryId: repo.RepoName, username: usuario, descripcion, page, prActives, nroIncidente, objetoGx);
            model.Environments = await _environmentService.GetEnvironmentDtosGxAsync(repo.RepoName);
            model.RepositoryId = repo.RepoName;
            model.Usuario = usuario;
            model.Descripcion = descripcion;
            model.NroIncidente = nroIncidente;
            model.ObjetoGx = objetoGx;
            model.SoloPendientes = true;

            return View(model);
        }

        [HttpGet("AddBuild")]
        [Authorize(Roles = "DesarrolloTecnico")]
        public async Task<IActionResult> AddBuild()
        {

            //var isAdmin = User.IsInRole("DesarrolloTecnico"); // No funciona con custom policies

            var apps = _configuration.GetSection("Projects").Get<List<ProjectSetting>>().Where(x => x.IsGx && x.Active).FirstOrDefault();
            var aplicativos = apps.Repos.Where(x => x.OwnBuild.HasValue && x.OwnBuild.Value);
            ViewBag.ReposBt = aplicativos;

            var tiposCambios = await _dbRepository.GetTiposCambios();
            var listaTc = new List<TiposCambio>
            {
                //new TiposCambio{ IdTipoCambio = 0, Descripcion = "Seleccione un valor"}
            };
            listaTc.AddRange(tiposCambios);
            ViewBag.TiposCambios = new SelectList(listaTc, "IdTipoCambio", "Descripcion");
            var m = new BuildGXViewModel()
            {
                IdTipoCambio = 1,
                IsHotfix = false
            };
            return View(m);
        }

        private async Task<bool> InternalRelease(BuildGXViewModelAndCommitsViewModel m)
        {
            m.modelCambio.BranchName = Guid.NewGuid().ToString();
            _azureService.ForceProyectType(m.modelCambio.AzureProjectType.ToAzureProjectType());
            var repositoryIdGx = m.modelCambio.BtRepo!;
            if (!await _azureService.CreateBranchAsync(repositoryIdGx, m.modelCambio.BranchName))
            {
                await _tempDataHelper.Message(TypeNotify.Error, "Imposible crear la rama para subir archivos...", "Error");
                return false;
            }

            var nameGx = string.Join("-", m.commits?.Select(x => x.revision!).Take(4)!);
            await _azureService.PushFiles(repositoryIdGx, m.modelCambio.BranchName, $"{m.modelCambio.Usuario}-{nameGx}-{DateTime.Now.ToString("yyyyMMddHHmmss")}.json", System.Text.Json.JsonSerializer.Serialize(m.commits));
            List<string> revIds = [];
            var iycRv = _configuration.GetReviewerConfig("IyC");
            revIds.Add(iycRv.FirstOrDefault()!.Id);
            var labels = await ArmarLabelsEnvioAsync(m.modelCambio.IsHotfix, m.modelCambio.IdTipoCambio, m.modelCambio.NroIncidente);
            var idPr = await _azureService.GeneratePullRequest(
                repositoryIdGx,
                m.modelCambio.Descripcion ?? string.Empty,
                m.modelCambio.Descripcion ?? string.Empty,
                m.modelCambio.BranchName,
                _configuration["AzureSettings:BranchPR"]!,
                false,
                labels);
            if (bool.Parse(_configuration["Testing:IncludeReviewersGx"]!))
            {
                await _azureService.AddReviewerToPR(repositoryIdGx, idPr, revIds.ToArray());

            }
            if (bool.Parse(_configuration["Testing:AutocompleteGX"]!))
            {
                await _azureService.EnableAutoCompleteForPR(repositoryIdGx, idPr);
            }

            m.modelCambio.IdPr = idPr;



            return true;
        }

        private async Task SendMail(int idPr, BuildsGx model)
        {
            // mailing
            if (idPr != 0)
            {
                var iycGroup = _configuration.GetReviewerConfig("IyC");
                var url = Url.Action("Vote", "GxDeploy", new { Id = model.IdBuildGx }, protocol: Request.Scheme);
                var title = $"ID {model.IdBuildGx}-{model.IdTipoCambioNavigation.Descripcion} {model.NroIncidente}";
                var additional_data = new Dictionary<string, string>
                    {
                        { "CreatedBy", model.Usuario },
                        { "CreationDate", DateTime.Now.ToString("dd/MM/yyyy HH:mm") },
                        { "SourceBranch", model.BranchName! },
                        { "TargetBranch", _configuration["AzureSettings:BranchPR"]! },
                        { "Description", model.Descripcion! },
                        { "TipoSolicitud", "Build GeneXus" },
                        { "PasajeId", model.IdBuildGx.ToString() },
                        { "TipoCambio", model.IdTipoCambioNavigation.Descripcion },
                        { "Incidente", model.NroIncidente.ToString() },
                        { "Hotfix", model.IsHotfix ? "Sí" : "No" }
                    };
                await _emailService.SendPullRequestNotificationIYCAsync("IYC", iycGroup.FirstOrDefault()!.Email!, idPr.ToString(), title, model.RepositoryId!
                    , url!, additional_data);
            }
        }

        // Refactorización para reducir la complejidad cognitiva de InternalSave
        private async Task<BuildsGx?> InternalSave(BuildGXViewModelAndCommitsViewModel m, bool forNew = true, bool release = false)
        {
            if (forNew)
            {
                var s = await _adGroupService.GetInfoUser();
                m.modelCambio.Usuario = s.Username;
                return await HandleSaveForNew(m, release);
            }
            else
            {
                return await HandleSaveForEdit(m, release);
            }
        }

        private async Task<BuildsGx?> HandleSaveForNew(BuildGXViewModelAndCommitsViewModel m, bool release)
        {
            if (release)
            {
                if (await InternalRelease(m))
                    return await _dbRepository.AddBuild(m);
                else
                    return null;
            }
            else
            {
                return await _dbRepository.AddBuild(m);
            }
        }

        private async Task<BuildsGx?> HandleSaveForEdit(BuildGXViewModelAndCommitsViewModel m, bool release)
        {
            if (release)
            {
                if (await InternalRelease(m))
                    return await _dbRepository.Edit(m);
                else
                    return null;
            }
            else
            {
                return await _dbRepository.Edit(m);
            }
        }

        private string ObtenerDisplayName(Type modelType, string propertyPath)
        {
            var parts = propertyPath.Split('.');
            var currentType = modelType;
            ModelMetadata? metadata = null;

            foreach (var part in parts)
            {
                metadata = _metadataProvider.GetMetadataForType(currentType);
                var propertyMetadata = metadata.Properties.FirstOrDefault(p => p.PropertyName == part);

                if (propertyMetadata == null)
                    return propertyPath; // fallback si no se encuentra la propiedad

                currentType = propertyMetadata.ModelType;
                metadata = propertyMetadata;
            }

            return metadata?.DisplayName ?? metadata?.PropertyName ?? propertyPath;
        }

        
        [HttpPost("guardar")]
        [Authorize(Roles = "DesarrolloTecnico")]
        public async Task<IActionResult> GuardarBuild([FromBody] BuildGXViewModelAndCommitsViewModel model)
        {
            if (!ModelState.IsValid || !model.commits.Any())
            {
                // Extraer errores
                var errores = ModelState
                    .Where(x => x.Value!.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => new
                        {
                            Label = ObtenerDisplayName(typeof(BuildGXViewModelAndCommitsViewModel), kvp.Key),
                            Errores = kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                        }
                    );

                return Json(new
                {
                    success = false,
                    message = "El modelo tiene errores de validación.",
                    errors = errores
                });
            }
            var r = await InternalSave(model);
            if (r != null)
            {
                await _tempDataHelper.Message(TypeNotify.Success);
                return Json(new { success = true, redirectUrl = Url.Action("index", "GxDeploy") });
            }
            // Guardás model y commits como quieras...
            return Json(new { success = false, message = "Error al guardar cambio. Contáctese con el administrador" });
        }



        [HttpPost("guardarYLiberar")]
        [Authorize(Roles = "DesarrolloTecnico")]
        public async Task<IActionResult> GuardarYLiberarBuild([FromBody] BuildGXViewModelAndCommitsViewModel model)
        {
            if (!ModelState.IsValid || !model.commits.Any())
            {
                // Extraer errores
                var errores = ModelState
                    .Where(x => x.Value!.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => new
                        {
                            Label = ObtenerDisplayName(typeof(BuildGXViewModelAndCommitsViewModel), kvp.Key),
                            Errores = kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                        }
                    );

                return Json(new
                {
                    success = false,
                    message = "El modelo tiene errores de validación.",
                    errors = errores
                });
            }
            var r = await InternalSave(model, forNew: true, release: true);
            if (r != null)
            {
                if (r.IdPr.HasValue)
                    await SendMail(r.IdPr.Value, r);
                await NotificarGuardadoGxAsync();
                return Json(new { success = true, redirectUrl = Url.Action("index", "GxDeploy") });
            }
            // Guardás model y commits como quieras...
            return Json(new { success = false, message = "Error al guardar cambio. Contáctese con el administrador" });
        }

        [HttpPut("guardar")]
        public async Task<IActionResult> EditGuardarBuild([FromBody] BuildGXViewModelAndCommitsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // Extraer errores
                var errores = ModelState
                    .Where(x => x.Value!.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => new
                        {
                            Label = ObtenerDisplayName(typeof(BuildGXViewModelAndCommitsViewModel), kvp.Key),
                            Errores = kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                        }
                    );

                return Json(new
                {
                    success = false,
                    message = "El modelo tiene errores de validación.",
                    errors = errores
                });
            }
            var r = await InternalSave(model, false);
            if (r != null)
            {
                await _tempDataHelper.Message(TypeNotify.Success);
                return Json(new { success = true, redirectUrl = Url.Action("index", "GxDeploy") });
            }
            // Guardás model y commits como quieras...
            return Json(new { success = false, message = "Error al guardar cambio. Contáctese con el administrador" });
        }

        [HttpPut("guardarYLiberar")]
        public async Task<IActionResult> EditGuardarYLiberarBuild([FromBody] BuildGXViewModelAndCommitsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // Extraer errores
                var errores = ModelState
                    .Where(x => x.Value!.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => new
                        {
                            Label = ObtenerDisplayName(typeof(BuildGXViewModelAndCommitsViewModel), kvp.Key),
                            Errores = kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                        }
                    );

                return Json(new
                {
                    success = false,
                    message = "El modelo tiene errores de validación.",
                    errors = errores
                });
            }
            var r = await InternalSave(model, false, true);
            if (r != null)
            {
                if (r.IdPr.HasValue)
                    await SendMail(r.IdPr.Value, r);
                await NotificarGuardadoGxAsync();
            }
            // Guardás model y commits como quieras...
            return Json(new { success = true, redirectUrl = Url.Action("index", "GxDeploy") });
        }

        [HttpGet("editar/{id}")]
        [Authorize(Roles = "DesarrolloTecnico")]
        public async Task<IActionResult> EditarBuild(int id)
        {
            var reposBt = _configuration.GetSection("Projects").Get<List<ProjectSetting>>()?.FirstOrDefault(x => x.Active && x.IsGx)?.Repos.Where(x => x.OwnBuild.HasValue && x.OwnBuild.Value);
            ViewBag.ReposBt = reposBt;
            var build = await _dbRepository.GetBuildById(id);
            if (build == null)
                return NotFound();
            var iu = await _adGroupService.GetInfoUser();
            if (iu.Username != build.Usuario)
                return Unauthorized();

            var usr = await _adGroupService.GetInfoUser();
            if (build.Usuario != usr.Username)
            {
                await _tempDataHelper.Message(TypeNotify.Error, "No se puede editar un cambio de otro analista", "Error con status code 401");
                return RedirectToAction("Index");
            }

            var tiposCambios = await _dbRepository.GetTiposCambios();

            ViewBag.TiposCambios = new SelectList(tiposCambios, "IdTipoCambio", "Descripcion", build.IdTipoCambio);

            var modelCambio = new BuildGXViewModel
            {
                IdBuildGx = build.IdBuildGx,
                Descripcion = build.Descripcion,
                Usuario = build.Usuario,
                FechaCreacion = build.FechaCreacion,
                IdTipoCambio = build.IdTipoCambio,
                NroIncidente = build.NroIncidente,
                BranchName = build.BranchName,
                IdPr = build.IdPr,
                BtRepo = build.RepositoryId,
                AzureProjectType = build.AzureProjectType,
                IsHotfix = build.IsHotfix,
                TareasJson = build.TareasJson
            };



            var commits = string.IsNullOrWhiteSpace(build.CommitListJson)
                ? new List<CommitsTeamDev>()
                : JsonSerializer.Deserialize<List<CommitsTeamDev>>(build.CommitListJson);

            var model = new BuildGXViewModelAndCommitsViewModel
            {
                modelCambio = modelCambio,
                commits = commits ?? new List<CommitsTeamDev>()
            };

            return View("EditarBuild", model);
        }

        [HttpPost("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DesarrolloTecnico")]
        public async Task<IActionResult> Delete([FromBody] int id)
        {            
            var r = await _dbRepository.DeleteGx(id);
            if (r)
            {
                await _tempDataHelper.Message(TypeNotify.Success);
                return Json(new
                {
                    success = true,
                    message = "Eliminado correctamente",
                    redirectUrl = Url.Action("Index", "GxDeploy")
                });
            }
            else
            {
                await _tempDataHelper.Message(TypeNotify.Error, "No se pudo eliminar el registro seleccionado");
                return Json(new
                {
                    success = false,
                    message = "No se pudo eliminar el registro seleccionado",
                    redirectUrl = Url.Action("Index", "GxDeploy")
                });
            }
        }

        [HttpPost("Descartar")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DesarrolloTecnico")]
        public async Task<IActionResult> Descartar([FromBody] int id)
        {
            try
            {
                var build = await _dbRepository.GetBuildById(id);
                if (build == null)
                    return Json(new { success = false, message = "Build no encontrado." });

                var usr = await _adGroupService.GetInfoUser();
                if (build.Usuario != usr.Username)
                {
                    _logger.LogWarning("Usuario {User} intentó descartar build GX {Id} de {Owner}", usr.Username, id, build.Usuario);
                    return Json(new { success = false, message = "Solo el creador puede anular este envío." });
                }

                if (!build.IdPr.HasValue || build.IdPr.Value == 0)
                {
                    return Json(new { success = false, message = "Sin Pull Request use Eliminar. Anular aplica solo cuando hay PR." });
                }

                if (string.IsNullOrEmpty(build.RepositoryId))
                {
                    return Json(new { success = false, message = "El build no tiene repositorio asociado." });
                }

                _azureService.ForceProyectType(build.AzureProjectType.ToAzureProjectType());

                var enProd = await _azureService.HasDeployedInProdAsync(build.IdPr.Value);
                if (enProd)
                {
                    return Json(new
                    {
                        success = false,
                        message = "No se puede anular porque el envío ya fue aplicado en Producción."
                    });
                }

                var azureOk = await _azureService.CancelarDeployActivoNoProdAsync(build.IdPr.Value, _logger);

                var discarded = await _dbRepository.Descartar(id, Helpers.Constants.Tables.BuildsGX, true);
                if (!discarded)
                {
                    return Json(new { success = false, message = "No se pudo anular el envío." });
                }

                await _azureService.AbandonarPullRequestAsync(build.RepositoryId, build.IdPr.Value);

                if (azureOk)
                    await _tempDataHelper.Message(TypeNotify.Success, "Envío anulado correctamente.");
                return Json(new
                {
                    success = true,
                    warning = azureOk ? null : AzureServiceExtensions.WarningReleaseNoCancelado
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al descartar build GX {Id}", id);
                return Json(new { success = false, message = "Error interno al anular el envío." });
            }
        }

        [HttpGet("Vote")]
        [Authorize(Roles = "IYC")]
        public async Task<IActionResult> Vote(int id)
        {
            var build = await _dbRepository.GetBuildById(id);
            if (build == null)
                return NotFound();

            var tiposCambios = await _dbRepository.GetTiposCambios();

            ViewBag.TiposCambios = new SelectList(tiposCambios, "IdTipoCambio", "Descripcion", build.IdTipoCambio);

            var modelCambio = new BuildGXViewModel
            {
                IdBuildGx = build.IdBuildGx,
                Descripcion = build.Descripcion,
                Usuario = build.Usuario,
                FechaCreacion = build.FechaCreacion,
                IdTipoCambio = build.IdTipoCambio,
                NroIncidente = build.NroIncidente,
                BranchName = build.BranchName,
                IdPr = build.IdPr,
                BtRepo = build.RepositoryId,
                AzureProjectType = build.AzureProjectType,
                IsHotfix = build.IsHotfix,
                TareasJson = build.TareasJson
            };

            var commits = string.IsNullOrWhiteSpace(build.CommitListJson)
                ? new List<CommitsTeamDev>()
                : JsonSerializer.Deserialize<List<CommitsTeamDev>>(build.CommitListJson);

            _azureService.ForceProyectType(modelCambio.AzureProjectType
                .ToAzureProjectType());
            var model = new BuildGXViewModelAndCommitsViewModel
            {
                modelCambio = modelCambio,
                commits = commits ?? new List<CommitsTeamDev>(),

                PullRequest = await _azureService.GetPullRequestAsync(build.IdPr ?? 0)
            };

            return View(model);
        }
        [HttpGet("ViewObjects")]
        public async Task<IActionResult> ViewObjects(int IdBuildGx)
        {
            var build = await _dbRepository.GetBuildById(IdBuildGx);
            if (build == null)
                return NotFound();

            var tiposCambios = await _dbRepository.GetTiposCambios();

            ViewBag.TiposCambios = new SelectList(tiposCambios, "IdTipoCambio", "Descripcion", build.IdTipoCambio);

            var modelCambio = new BuildGXViewModel
            {
                IdBuildGx = build.IdBuildGx,
                Descripcion = build.Descripcion,
                Usuario = build.Usuario,
                FechaCreacion = build.FechaCreacion,
                IdTipoCambio = build.IdTipoCambio,
                NroIncidente = build.NroIncidente,
                BranchName = build.BranchName,
                IdPr = build.IdPr,
                BtRepo = build.RepositoryId,
                AzureProjectType = build.AzureProjectType,
                IsHotfix = build.IsHotfix,
                TareasJson = build.TareasJson
            };

            var commits = string.IsNullOrWhiteSpace(build.CommitListJson)
                ? new List<CommitsTeamDev>()
                : JsonSerializer.Deserialize<List<CommitsTeamDev>>(build.CommitListJson);

            _azureService.ForceProyectType(modelCambio.AzureProjectType.ToAzureProjectType());
            var model = new BuildGXViewModelAndCommitsViewModel
            {
                modelCambio = modelCambio,
                commits = commits ?? new List<CommitsTeamDev>(),
                PullRequest = await _azureService.GetPullRequestAsync(build.IdPr ?? 0)
            };

            return View(model);
        }

        [HttpPost("Vote")]
        public async Task<IActionResult> Vote(BuildGXViewModelAndCommitsViewModel model)
        {
            var iu = await _adGroupService.GetInfoUser();
            //obtener el pat
            var pat = await _patRepositoryService.GetPatByUser(iu.Username);
            var prId = (model.modelCambio.IdPr.HasValue) ? model.modelCambio.IdPr.Value : 0;

            try
            {
                _azureService.ForceProyectType(model.modelCambio.AzureProjectType.ToAzureProjectType());
                var repositoryIdGx = model.modelCambio.BtRepo!;
                await _azureService.VoteOnPullRequestWithPatAsync(pat!, iu.Username, repositoryIdGx, prId, model.Vote!.Value, model.Comment);
                if (model.Vote < 0)
                {
                    //rechazo. Abandono PR y guardo en la db
                    await _azureService.AbandonarPullRequestAsync(repositoryIdGx, prId);
                    
                }
                await _tempDataHelper.Message(TypeNotify.Success);
                return RedirectToAction("index", "GxDeploy");
            }
            catch (Exception ex)
            {
                await _tempDataHelper.Message(TypeNotify.Error, ex.Message, "Error al intentar votar PR");
                return RedirectToAction("index", "GxDeploy");
            }

        }

        private async Task<IReadOnlyList<string>> ArmarLabelsEnvioAsync(bool isHotfix, int idTipoCambio, int nroIncidente)
        {
            var tipos = await _dbRepository.GetTiposCambios();
            var tipoDesc = tipos.FirstOrDefault(t => t.IdTipoCambio == idTipoCambio)?.Descripcion;
            if (string.IsNullOrWhiteSpace(tipoDesc))
            {
                _logger.LogWarning(
                    "No se resolvió TiposCambio.Descripcion para {IdTipoCambio}; se usará solo el número {NroIncidente}",
                    idTipoCambio, nroIncidente);
            }

            return PullRequestLabelHelper.ArmarLabels(isHotfix, tipoDesc, nroIncidente);
        }

        private Task NotificarGuardadoGxAsync()
        {
            if (_azureService.LastLabelsFailed)
            {
                return _tempDataHelper.Message(
                    TypeNotify.Warning,
                    $"Los cambios han sido guardados exitosamente. {PullRequestLabelHelper.MensajeEtiquetasNoAplicadas}",
                    "Cambios");
            }

            return _tempDataHelper.Message(TypeNotify.Success);
        }
    }
}
