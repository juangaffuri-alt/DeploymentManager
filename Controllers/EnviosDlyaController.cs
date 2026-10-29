using Azure.Core;
using Castle.Components.DictionaryAdapter.Xml;
using DocumentFormat.OpenXml.Office2016.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;
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
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PasadorScript.Controllers
{
    [Route("EnviosDlya")]
    [Authorize]
    [RequireValidPat]
    public class EnviosDlyaController : Controller
    {

        private readonly IDbRepository _dbRepository;
        private readonly ILogger<EnviosDlyaController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IAdGroupService _adGroupService;
        private readonly TempDataHelper _tempDataHelper;
        private readonly IPatRepositoryService _patRepositoryService;
        private readonly IAzureReleaseService _azureReleaseService;
        private readonly IAzureService _azureService;
        private readonly NuevoP229Context _context;
        private readonly IEmailService _emailService;
        private readonly IEnvironmentService _environmentService;

        public EnviosDlyaController(ILogger<EnviosDlyaController> logger, IDbRepository dbRepository,
            IConfiguration configuration, IAdGroupService adGroupService,TempDataHelper tempDataHelper,
            IPatRepositoryService patRepositoryService, IAzureReleaseService azureReleaseService, 
            IAzureService azureService, NuevoP229Context context, IEmailService emailService,
            IEnvironmentService environmentService)
        {
            _dbRepository = dbRepository;
            _logger = logger;
            _configuration = configuration;
            _adGroupService = adGroupService;
            _tempDataHelper = tempDataHelper;
            _patRepositoryService = patRepositoryService;
            _azureReleaseService = azureReleaseService;
            _azureService = azureService;
            _context = context;
            _emailService = emailService;
            _environmentService = environmentService;
        }
        public async Task<IActionResult> Index(string? usuario, string? descripcion, string? repositoryId, int page = 1, int idEstado = 0, bool deleted = false, int? nroIncidente = null)
        {
            usuario = await UsuarioFiltroHelper.ResolverAsync(usuario, Request.Query, _adGroupService);
            var aplicativos = _configuration.GetSection("Projects").Get<List<ProjectSetting>>()?.Where(x => x.Active && x.IsGx).FirstOrDefault()?.Repos.Where(x => x.OwnBuild.HasValue && !x.OwnBuild.Value);
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
            
            var model = await _dbRepository.GetEnviosDlyaAsync(deleted, usuario, descripcion, repo.RepoName, page, nroIncidente);
            model.Environments = await _environmentService.GetEnvironmentDtosGxAsync(repo.RepoName, false);
            model.RepositoryId = repo.RepoName;
            model.Descripcion = descripcion;
            model.Usuario = usuario;
            model.Deleted = deleted;
            model.NroIncidente = nroIncidente;
            
            return View(model);
        }
        [Authorize(Roles = "DesarrolloFuncional, DesarrolloTecnico")]
        [HttpGet("AddEnvioDlya")]
        public async Task<IActionResult> AddEnvioDlya()
        {

            var aplicativos = _configuration.GetSection("Projects").Get<List<ProjectSetting>>()?.Where(x => x.Active && x.IsGx).FirstOrDefault()?.Repos.Where(x => x.OwnBuild.HasValue && !x.OwnBuild.Value);
            ViewBag.Aplicativos = new SelectList(aplicativos, "RepoName", "DisplayName");

            var tiposCambios = await _dbRepository.GetTiposCambios();

            ViewBag.TiposCambios = new SelectList(tiposCambios, "IdTipoCambio", "Descripcion");

            var m = new EnvioDlyaRequestViewModel();
            m.Tipo = 3;
            return View(m);
        }

        private List<VirtualFileViewModel>? FormalizarArchivos(EnvioDlyaRequestViewModel model)
        {
            var list = JsonSerializer.Deserialize<List<VirtualFileViewModel>>(model.JsonVirtualFiles);
            return list;
        }

        [HttpPost("AddNewEnvioAndCreatePR")]
        public async Task<IActionResult> AddNewEnvioAndCreatePR(EnvioDlyaRequestViewModel model)
        {

            // Procesar archivos normales
            //var virtualFiles = JsonSerializer.Deserialize<List<VirtualFile>>(model.JsonVirtualFiles);

            // Si hay datos de ZIP (cuando se presiona "Guardar y liberar")
            if (!string.IsNullOrEmpty(model.JsonVirtualFiles))
            {
                model.FilesRequest = FormalizarArchivos(model);
                var list = JsonSerializer.Deserialize<List<VirtualFileViewModelBase>>(model.JsonVirtualFiles);
                model.TimeStampJson = JsonSerializer.Serialize(list?.Select(x => new { x.Name, x.Fecha }));

                // A partir de acá podés procesar virtualFiles como quieras
                var r = await InternalSave(model, forNew: true, release: true);
                if (r != null)
                {
                    var msgPr = r.IdPr.HasValue ? $"PullRequest: {r.IdPr.Value}" : "";
                    await SendMail(r.IdPr!.Value, r);
                    await NotificarGuardadoAsync($"Cambios guardados correctamente. {msgPr}");
                    return RedirectToAction("Index", "EnviosDlya");
                }
                return View(model);
            }
            return View(model);
        }

        [HttpPost("AddNewEnvioDlya")]

        public async Task<IActionResult> AddNewEnvioDlya(EnvioDlyaRequestViewModel model)
        {

            // Procesar archivos normales
            //var virtualFiles = JsonSerializer.Deserialize<List<VirtualFile>>(model.JsonVirtualFiles);

            // Si hay datos de ZIP (cuando se presiona "Guardar y liberar")

            if (!string.IsNullOrEmpty(model.JsonVirtualFiles))
            {
                model.FilesRequest = FormalizarArchivos(model);
                var list = JsonSerializer.Deserialize<List<VirtualFileViewModelBase>>(model.JsonVirtualFiles);
                model.TimeStampJson = JsonSerializer.Serialize(list?.Select(x => new { x.Name, x.Fecha }));

                // A partir de acá podés procesar virtualFiles como quieras
                var r = await InternalSave(model, forNew: true, release: false);
                if (r != null)
                {
                    var msgPr = r.IdPr.HasValue ? $"PullRequest: {r.IdPr.Value}" : "";
                    await _tempDataHelper.Message(TypeNotify.Success, $"Cambios guardados correctamente. {msgPr}", "Cambios");
                    return RedirectToAction("Index", "EnviosDlya");
                }
                return View(model);
            }
            return View(model);
        }

        private async Task<bool> InternalRelease(EnvioDlyaRequestViewModel m, bool generarPR)
        {
            m.FolderAndBranchNameDesa = Guid.NewGuid().ToString();
            _azureService.ForceProyectType(m.AzureProjectType.ToAzureProjectType());
            if (!await _azureService.CreateBranchAsync(m.RepositoryId, m.FolderAndBranchNameDesa))
            {
                await _tempDataHelper.Message(TypeNotify.Error, "Imposible crear la rama para subir archivos...", "Error");
                return false;
            }

            m.FilesRequest.Add(new VirtualFileViewModel()
            {
                Name = "timestamps.json",
                Content = m.TimeStampJson,
                Estado = "nuevo"
            });

            await _azureService.PushFiles(m.RepositoryId, m.FolderAndBranchNameDesa, m.FilesRequest.ToList(), true);
            if (generarPR)
            {
                List<string> revIds = [];
                var iycRv = _configuration.GetReviewerConfig("IyC");
                revIds.Add(iycRv.FirstOrDefault()!.Id);
                var labels = await ArmarLabelsEnvioAsync(m.IsHotfix, m.Tipo, m.Numero);
                int? idPr = await _azureService.GeneratePullRequest(
                    m.RepositoryId,
                    m.Descripcion,
                    m.Descripcion,
                    m.FolderAndBranchNameDesa!,
                    _configuration["AzureSettings:BranchPR"]!,
                    true,
                    labels);


                if (bool.Parse(_configuration["Testing:IncludeReviewersGx"]!))
                {
                    await _azureService.AddReviewerToPR(m.RepositoryId, idPr.Value, revIds.ToArray());

                }
                if (bool.Parse(_configuration["Testing:AutocompleteGX"]!))
                {
                    await _azureService.EnableAutoCompleteForPR(m.RepositoryId, idPr ?? 0);
                }

                m.PullRequestId = idPr;
            }
            return true;
        }

        private async Task<EnviosDlya?> InternalSave(EnvioDlyaRequestViewModel m, bool forNew = true, bool release = false)
        {
            if (forNew)
            {
                var s = await _adGroupService.GetInfoUser();
                m.Usuario = s.Username;
                if (await InternalRelease(m, release))
                    return await _dbRepository.AddEnvioDlya(m);
                else
                    return null;
            }
            else
            {
                if (await InternalRelease(m, release))
                    return await _dbRepository.AddEnvioDlya(m);
                else
                    return null;

            }
        }
        [Authorize(Roles = "DesarrolloFuncional, DesarrolloTecnico")]
        [HttpGet("Edit")]
        public async Task<IActionResult> Edit(int id, string branchSource)
        {
            var model = await _dbRepository.EnvioDlyaToRequestViewModel(id);

            var usr = await _adGroupService.GetInfoUser();
            if (model?.Usuario != usr.Username)
            {
                await _tempDataHelper.Message(TypeNotify.Error, "No se puede editar un cambio de otro analista", "Error con status code 401");
                return RedirectToAction("Index");
            }

            var aplicativos = _configuration.GetSection("Projects").Get<List<ProjectSetting>>()?.FirstOrDefault(x=>x.Active && x.IsGx)?.Repos.Where(x=>x.OwnBuild.HasValue && !x.OwnBuild.Value);
            ViewBag.Aplicativos = new SelectList(aplicativos, "RepoName", "DisplayName");

            var tiposCambios = await _dbRepository.GetTiposCambios();

            ViewBag.TiposCambios = new SelectList(tiposCambios, "IdTipoCambio", "Descripcion");

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            var list = JsonSerializer.Deserialize<List<VirtualFileViewModelFromDb>>(model?.JsonVirtualFiles!, options);
            foreach (var f in list!)
            {
                model?.Files.Add(new FileView()
                {
                    FileName = f.Name,
                    IdFileAzure = $"{f.Name}",
                    Content = f.Content,
                    DisplayName = f.DisplayName,
                    FechaCreacion = f._Fecha,                    
                });
            }

            return View(model);
        }

        private void PeluquearPathNuevosArchivos(ICollection<VirtualFileViewModel> files)
        {
            foreach (var file in files)
            {
                if (file.Estado == "nuevo")
                {
                    file.Name = file.Name.RemoveFirstFolder().Replace(Path.DirectorySeparatorChar, '/'); //path + nombre
                }
            }
        }

        private async Task<bool> ProcesarEditEnvioDlya(EnvioDlyaRequestViewModel request, bool generarPR)
        {
            _azureService.ForceProyectType(request.AzureProjectType.ToAzureProjectType());
            int? idPr = null;
            var estados = new[] { "modificado", "nuevo", "eliminado" };
            request.FilesRequest = JsonSerializer.Deserialize<ICollection<VirtualFileViewModel>>(request.JsonVirtualFiles);
            var list = JsonSerializer.Deserialize<List<VirtualFileViewModelBase>>(request.JsonVirtualFiles);
            request.TimeStampJson = JsonSerializer.Serialize(list?.Select(x => new { x.Name, x.Fecha }).Where(x=>x.Name!= "timestamps.json")); 
            request.FilesRequest.Add(new VirtualFileViewModel()
            {
                Name = "timestamps.json",
                Content = request.TimeStampJson,
                Estado = "modificado"
            });
            //PeluquearPathNuevosArchivos(request.FilesRequest);
            if (request.FilesRequest.Any(x => estados.Contains(x.Estado)))
            {
                await _azureService.PushFiles(request.RepositoryId, request.FolderAndBranchNameDesa!, request.FilesRequest.ToList(), false);
            }

            if (generarPR)
            {
                List<string> revIds = [];
                var iycRv = _configuration.GetReviewerConfig("IyC");
                revIds.Add(iycRv.FirstOrDefault()!.Id);

                var labels = await ArmarLabelsEnvioAsync(request.IsHotfix, request.Tipo, request.Numero);
                idPr = await _azureService.GeneratePullRequest(
                    request.RepositoryId,
                    request.Descripcion,
                    request.Descripcion,
                    request.FolderAndBranchNameDesa!,
                    _configuration["AzureSettings:BranchPR"]!,
                    true,
                    labels);

                if (bool.Parse(_configuration["Testing:IncludeReviewersGx"]!))
                {
                    await _azureService.AddReviewerToPR(request.RepositoryId, idPr.Value, revIds.ToArray());

                }
                if (bool.Parse(_configuration["Testing:AutocompleteGX"]!))
                {
                    await _azureService.EnableAutoCompleteForPR(request.RepositoryId, idPr ?? 0);
                }

                request.PullRequestId = idPr ?? 0;
            }

            var res = await _dbRepository.UpdatePasaje(request);

            if (res != null)
            {
                if (res.IdPr.HasValue)
                    await SendMail(res.IdPr!.Value, res);
                var msgPr = idPr != null ? $"PullRequest: {idPr}" : "";
                await NotificarGuardadoAsync($"Cambios guardados correctamente. {msgPr}");
            }
            else
            {
                await _tempDataHelper.Message(TypeNotify.Error, "Error al guardar scripts", "Error");
            }

            return true;
        }

        [HttpPost("EditEnvioDlyaAndCreatePR")]
        public async Task<IActionResult> EditEnvioDlyaAndCreatePR(EnvioDlyaRequestViewModel request)
        {
            if (ModelState.IsValid)
            {
                await ProcesarEditEnvioDlya(request, generarPR: true);
            }
            return RedirectToAction("Index");
        }

        [HttpPost("EditEnvioDlya")]
        public async Task<IActionResult> EditEnvioDlya(EnvioDlyaRequestViewModel request)
        {
            if (ModelState.IsValid)
            {
                await ProcesarEditEnvioDlya(request, generarPR: false);
            }
            return RedirectToAction("Index");
        }

        [HttpGet("Vote")]
        public async Task<IActionResult> Vote(int id)
        {
            var build = await _dbRepository.GetEnvioDlya(id);
            if (build == null)
                return NotFound();

            var tiposCambios = await _dbRepository.GetTiposCambios();

            ViewBag.TiposCambios = new SelectList(tiposCambios, "IdTipoCambio", "Descripcion", build.IdTipoCambio);

            var modelCambio = new EnvioDlyaViewModel
            {
                IdEnvioDlya = build.IdEnvioDlya,
                Descripcion = build.Descripcion,
                Usuario = build.Usuario,
                FechaCreacion = build.FechaCreacion,
                IdTipoCambio = build.IdTipoCambio,
                NroIncidente = build.NroIncidente,
                BranchName = build.BranchName,
                IdPr = build.IdPr,
                RepositoryId = build.RepositoryId,
                AzureProjectType = build.AzureProjectType,
                DescripcionTipoCambio = build.IdTipoCambioNavigation.Descripcion??"",
                TareasJson = build.TareasJson
            };

            AzureDevopsPullRequest? pr = null;
            _azureService.ForceProyectType(modelCambio.AzureProjectType.ToAzureProjectType());
            if (build.IdPr.HasValue)
                pr = await _azureService.GetPullRequestAsync(build.IdPr.Value);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var commits = (string.IsNullOrWhiteSpace(build.CommitListJson)
                        ? new List<CommitsEnviosDlya>()
                        : JsonSerializer.Deserialize<List<CommitsEnviosDlya>>(build.CommitListJson, options))
                        ?.Where(c => c.Name != "timestamps.json").ToList();

            var model = new EnvioDlyaViewModelAndCommitsViewModel
            {
                modelCambio = modelCambio,
                commits = commits ?? new List<CommitsEnviosDlya>(),
                PullRequest = pr
            };

            return View(model);
        }
        [HttpGet("ViewObjects")]
        public async Task<IActionResult> ViewObjects(int IdBuildGx)
        {
            var build = await _dbRepository.GetEnvioDlya(IdBuildGx);
            if (build == null)
                return NotFound();

            var tiposCambios = await _dbRepository.GetTiposCambios();

            ViewBag.TiposCambios = new SelectList(tiposCambios, "IdTipoCambio", "Descripcion", build.IdTipoCambio);

            var modelCambio = new EnvioDlyaViewModel
            {
                IdEnvioDlya = build.IdEnvioDlya,
                Descripcion = build.Descripcion,
                Usuario = build.Usuario,
                FechaCreacion = build.FechaCreacion,
                IdTipoCambio = build.IdTipoCambio,
                NroIncidente = build.NroIncidente,
                BranchName = build.BranchName,
                IdPr = build.IdPr,
                RepositoryId = build.RepositoryId,
                AzureProjectType = build.AzureProjectType,
                TareasJson = build.TareasJson
            };
            AzureDevopsPullRequest? pr = null;
            _azureService.ForceProyectType(modelCambio.AzureProjectType.ToAzureProjectType());
            if (build.IdPr.HasValue)
                pr = await _azureService.GetPullRequestAsync(build.IdPr.Value);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var commits = (string.IsNullOrWhiteSpace(build.CommitListJson)
                        ? new List<CommitsEnviosDlya>()
                        : JsonSerializer.Deserialize<List<CommitsEnviosDlya>>(build.CommitListJson, options))
                        ?.Where(c => c.Name != "timestamps.json").ToList();


            var model = new EnvioDlyaViewModelAndCommitsViewModel
            {
                modelCambio = modelCambio,
                commits = commits ?? new List<CommitsEnviosDlya>(),
                PullRequest = pr
            };

            return View(model);
        }

        [HttpPost("Vote")]
        public async Task<IActionResult> Vote(EnvioDlyaViewModelAndCommitsViewModel model)
        {
            var iu = await _adGroupService.GetInfoUser();
            //obtener el pat
            var pat = await _patRepositoryService.GetPatByUser(iu.Username);
            var prId = (model.modelCambio.IdPr.HasValue) ? model.modelCambio.IdPr.Value : 0;

            try
            {
                var repoId = model.modelCambio.RepositoryId!;
                _azureService.ForceProyectType(model.modelCambio.AzureProjectType.ToAzureProjectType());
                //_serviciosAzureGx.RepositoryIdGx = ;
                await _azureService.VoteOnPullRequestWithPatAsync(pat!, iu.Username,repoId, prId, model.Vote!.Value, model.Comment);
                if (model.Vote < 0)
                {
                    //rechazo. Abandono PR y guardo en la db
                    await _azureService.AbandonarPullRequestAsync(repoId, prId);
                    model.modelCambio.IdPr = null;
                    await _dbRepository.UpdatePasaje(model);
                }
                //else
                //{
                //    await _serviciosAzureGx.CompletePullRequestAsync(_serviciosAzureGx.RepositoryIdGx, prId);
                //}
                await _tempDataHelper.Message(TypeNotify.Success);
                return RedirectToAction("index", "EnviosDlya");
            }
            catch (Exception ex)
            {
                await _tempDataHelper.Message(TypeNotify.Error, ex.Message, "Error al intentar votar PR");
                return RedirectToAction("index", "EnviosDlya");
            }

        }

        [HttpPost("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete([FromBody] int id)
        {
            var r = await _dbRepository.DeleteEnvioDlya(id);
            if (r)
            {
                await _tempDataHelper.Message(TypeNotify.Success);
                return Json(new
                {
                    success = true,
                    message = "Eliminado correctamente",
                    redirectUrl = Url.Action("Index", "EnviosDlya")
                });
            }
            else
            {
                await _tempDataHelper.Message(TypeNotify.Error, "No se pudo eliminar el registro seleccionado");
                return Json(new
                {
                    success = false,
                    message = "No se pudo eliminar el registro seleccionado",
                    redirectUrl = Url.Action("Index", "EnviosDlya")
                });
            }
            
        }

        [HttpPost("Descartar")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DesarrolloFuncional, DesarrolloTecnico")]
        public async Task<IActionResult> Descartar([FromBody] int id)
        {
            try
            {
                var envio = await _dbRepository.GetEnvioDlya(id);
                if (envio == null)
                    return Json(new { success = false, message = "Envío no encontrado." });

                var usr = await _adGroupService.GetInfoUser();
                if (envio.Usuario != usr.Username)
                {
                    _logger.LogWarning("Usuario {User} intentó descartar envío DLYA {Id} de {Owner}", usr.Username, id, envio.Usuario);
                    return Json(new { success = false, message = "Solo el creador puede anular este envío." });
                }

                if (!envio.IdPr.HasValue || envio.IdPr.Value == 0)
                {
                    return Json(new { success = false, message = "Sin Pull Request use Eliminar. Anular aplica solo cuando hay PR." });
                }

                if (string.IsNullOrEmpty(envio.RepositoryId))
                {
                    return Json(new { success = false, message = "El envío no tiene repositorio asociado." });
                }

                _azureService.ForceProyectType(envio.AzureProjectType.ToAzureProjectType());

                var enProd = await _azureService.HasDeployedInProdAsync(envio.IdPr.Value);
                if (enProd)
                {
                    return Json(new
                    {
                        success = false,
                        message = "No se puede anular porque el envío ya fue aplicado en Producción."
                    });
                }

                var azureOk = await _azureService.CancelarDeployActivoNoProdAsync(envio.IdPr.Value, _logger);

                var discarded = await _dbRepository.Descartar(id, Helpers.Constants.Tables.EnviosDlya, true);
                if (!discarded)
                {
                    return Json(new { success = false, message = "No se pudo anular el envío." });
                }

                await _azureService.AbandonarPullRequestAsync(envio.RepositoryId, envio.IdPr.Value);

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
                _logger.LogError(ex, "Error al descartar envío DLYA {Id}", id);
                return Json(new { success = false, message = "Error interno al anular el envío." });
            }
        }

        [HttpGet("RedirectToAudit")]
        public async Task<IActionResult> RedirectToAudit(int idEnvioDlya)
        {
            try
            {
                
                // Obtener el script por ID
                var buildGx = await _dbRepository.GetEnvioDlya(idEnvioDlya);

                if (buildGx == null || !buildGx.IdPr.HasValue)
                {
                    await _tempDataHelper.Message(TypeNotify.Error, "No se encontró el envío dlya o no tiene Pull Request asociado", "Error");
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
                        projectType = buildGx.AzureProjectType,
                        returnUrl = Url.Action("Index", "EnviosDlya")
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

                ViewBag.ReturnUrl = Url.Action("Index", "EnviosDlya");
                return View("~/Views/Release/AuditView.cshtml", audit);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error redirigiendo a auditoría para envioDlya {idEnvioDlya}", idEnvioDlya);
                await _tempDataHelper.Message(TypeNotify.Error, "Error al cargar el historial", "Error");
                return RedirectToAction("Index");
            }
        }
        private async Task SendMail(int idPr, EnviosDlya model)
        {
            // mailing
            if (idPr != 0)
            {
                var iycGroup = _configuration.GetReviewerConfig("IyC");
                var url = Url.Action("Vote", "EnviosDlya", new { Id = model.IdEnvioDlya }, protocol: Request.Scheme);
                var title = $"ID {model.IdEnvioDlya}-{model.IdTipoCambioNavigation.Descripcion} {model.NroIncidente}";
                var additional_data = new Dictionary<string, string>
                    {
                        { "CreatedBy", model.Usuario },
                        { "CreationDate", DateTime.Now.ToString("dd/MM/yyyy HH:mm") },
                        { "SourceBranch", model.BranchName! },
                        { "TargetBranch", _configuration["AzureSettings:BranchPR"]! },
                        { "Description", model.Descripcion! },
                        { "TipoSolicitud", "Envío DLYA" },
                        { "PasajeId", model.IdEnvioDlya.ToString() },
                        { "TipoCambio", model.IdTipoCambioNavigation.Descripcion },
                        { "Incidente", model.NroIncidente.ToString() },
                        { "Hotfix", model.IsHotfix ? "Sí" : "No" }
                    };
                await _emailService.SendPullRequestNotificationIYCAsync("IYC", iycGroup.FirstOrDefault()!.Email!, idPr.ToString(), title, model.RepositoryId!
                    , url!, additional_data);
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

        private Task NotificarGuardadoAsync(string mensajeExito)
        {
            if (_azureService.LastLabelsFailed)
            {
                return _tempDataHelper.Message(
                    TypeNotify.Warning,
                    $"{mensajeExito} {PullRequestLabelHelper.MensajeEtiquetasNoAplicadas}",
                    "Cambios");
            }

            return _tempDataHelper.Message(TypeNotify.Success, mensajeExito, "Cambios");
        }
    }
}
