using Azure.Core;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PasadorScript.DbModels;
using PasadorScript.DTO.Auditoria; // <-- añadido
using PasadorScript.DTO.AzureDev;
using PasadorScript.DTO.Config;
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
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SqlStructureInfo = PasadorScript.ViewModels.SqlStructureInfo;

namespace PasadorScript.Controllers
{
    [RequireValidPat]
    [Authorize]
    public class SqlFilesController : Controller
    {
        private readonly IEmailService _emailService;
        private readonly ILogger<SqlFilesController> _logger;
        private readonly IDbRepository _repo;
        private readonly IAdGroupService _adGroupService;
        private readonly IConfiguration _configuration;
        private readonly TempDataHelper _tempDataHelper;
        private readonly IPatRepositoryService _patRepositoryService;
        private readonly IAzureService _azureService;
        private readonly NuevoP229Context _dbContext;
        private readonly IEnvironmentService _environmentService;
        private const string FiltroAplicativoTodos = "TODOS";
        private const string EstadosReleasePendientes = "queued,pending,inProgress";

        public SqlFilesController(ILogger<SqlFilesController> logger, IAzureService azureService,
            IDbRepository context, IAdGroupService adGroupService, IConfiguration configuration,
            TempDataHelper tempDataHelper, IPatRepositoryService patRepositoryService, NuevoP229Context dbContext,
            IEmailService emailService, IEnvironmentService environmentService)
        {
            _emailService = emailService;
            _logger = logger;
            _repo = context;
            _adGroupService = adGroupService;
            _configuration = configuration;
            _tempDataHelper = tempDataHelper;
            _patRepositoryService = patRepositoryService;
            _azureService = azureService;
            _azureService.ForceProyectType(AzureProjectType.Default);
            _dbContext = dbContext;
            _environmentService = environmentService;
        }

        [HttpGet]
        public async Task<IActionResult> RedirectToAudit(int idScript)
        {
            try
            {
                var script = await _repo.ScriptToViewModel(idScript);

                if (script == null || !script.PullRequestIdQA.HasValue)
                {
                    await _tempDataHelper.Message(TypeNotify.Error, "No se encontró el script o no tiene Pull Request asociado", "Error");
                    return RedirectToAction("Index");
                }

                var prId = script.PullRequestIdQA.Value;

                _azureService.ForceProyectType(script.AzureProjectType.ToAzureProjectType());

                // Intentar obtener release desde PR (flujo original)
                var release = await _azureService.ObtenerReleaseDesdePullRequestAsync(prId);
                if (release != null)
                {
                    return RedirectToAction("Auditoria", "Release", new
                    {
                        releaseId = release.Id,
                        pullRequestId = prId,
                        projectType = script.AzureProjectType,
                        returnUrl = Url.Action("Index", "SqlFiles")
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

                ViewBag.ReturnUrl = Url.Action("Index", "SqlFiles");
                return View("~/Views/Release/AuditView.cshtml", audit);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error redirigiendo a auditoría para script {IdScript}", idScript);
                await _tempDataHelper.Message(TypeNotify.Error, "Error al cargar el historial", "Error");
                return RedirectToAction("Index");
            }
        }

        private async Task<SearchScripts> GetMainModel(string? usuario, string? descripcion, string? repositoryId, bool? deleted = false, int page = 1, int idEstado = 0, int? nroIncidente = null)
        {
            usuario = await UsuarioFiltroHelper.ResolverAsync(usuario, Request.Query, _adGroupService);
            var repos = ObtenerReposScriptsActivos();
            CargarComboAplicativos(repos);

            if (repos.Count == 0)
            {
                _logger.LogWarning("No hay proyecto de scripts activo o no hay repositorios configurados");
                var modelVacio = await _repo.GetScripts(deleted, usuario, descripcion, null, page, idEstado, prIds: [], nroIncidente: nroIncidente);
                CompletarModeloBusqueda(
                    modelVacio,
                    usuario,
                    descripcion,
                    FiltroAplicativoTodos,
                    deleted.HasValue && deleted.Value,
                    idEstado,
                    nroIncidente,
                    soloPendientes: false);
                return modelVacio;
            }

            var (filtroSql, repoEntornos, modelRepositoryId) = ResolverFiltroAplicativo(repositoryId, repos);
            var model = await _repo.GetScripts(deleted, usuario, descripcion, filtroSql, page, idEstado, nroIncidente: nroIncidente);
            CompletarModeloBusqueda(
                model,
                usuario,
                descripcion,
                modelRepositoryId,
                deleted.HasValue && deleted.Value,
                idEstado,
                nroIncidente,
                soloPendientes: false);
            model.Environments = await _environmentService.GetEnvironmentDtosScriptsAsync(repoEntornos.RepoName);
            return model;
        }

        [HttpGet]
        public async Task<ActionResult<SearchScripts>> Index(string? usuario, string? descripcion, string? repositoryId, bool deleted = false, int page = 1, int idEstado = 0, int? nroIncidente = null)
        {
            var model = await GetMainModel(usuario, descripcion, repositoryId, deleted, page, idEstado, nroIncidente);
            return View(model);
        }

        [HttpGet]
        public IActionResult Deleted(string? usuario, string? descripcion, string? repositoryId, int page = 1, int idEstado = 0, int? nroIncidente = null)
        {
            var values = new RouteValueDictionary
            {
                ["descripcion"] = descripcion,
                ["repositoryId"] = repositoryId,
                ["page"] = page,
                ["idEstado"] = idEstado,
                ["deleted"] = true,
                ["nroIncidente"] = nroIncidente
            };

            if (Request.Query.ContainsKey(UsuarioFiltroHelper.QueryKeyUsuario))
                values["usuario"] = usuario ?? string.Empty;

            if (Request.Query.ContainsKey(UsuarioFiltroHelper.QueryKeyUsuario)
                || UsuarioFiltroHelper.EsFlagExplicit(Request.Query))
            {
                values["usuarioExplicit"] = true;
            }

            return RedirectToAction(nameof(Index), values);
        }

        [HttpGet]
        public async Task<ActionResult<SearchScripts>> Pendientes(string? usuario, string? descripcion, string? repositoryId, int page = 1, int idEstado = 0, int? nroIncidente = null)
        {
            usuario = await UsuarioFiltroHelper.ResolverAsync(usuario, Request.Query, _adGroupService);
            var (reposActivos, reposOld) = ObtenerReposScriptsActivosYOld();
            CargarComboAplicativos(reposActivos);

            if (reposActivos.Count == 0)
            {
                _logger.LogWarning("No hay proyecto de scripts activo o no hay repositorios configurados");
                var modelVacio = await _repo.GetScripts(false, usuario, descripcion, null, page, idEstado, prIds: [], nroIncidente);
                CompletarModeloBusqueda(
                    modelVacio,
                    usuario,
                    descripcion,
                    FiltroAplicativoTodos,
                    deleted: false,
                    idEstado,
                    nroIncidente,
                    soloPendientes: true);
                return View(modelVacio);
            }

            var prActives = new List<int>();
            string? filtroSql;
            RepoSetting repoEntornos;
            string modelRepositoryId;
            var esTodos = EsFiltroTodos(repositoryId);
            RepoSetting? repoConcreto = null;

            if (!esTodos)
            {
                repoConcreto = reposActivos.FirstOrDefault(x => x.RepoName == repositoryId);
                if (repoConcreto is null)
                {
                    _logger.LogWarning("Aplicativo desconocido {RepositoryId}; se usa el filtro TODOS", repositoryId);
                    esTodos = true;
                }
            }

            if (esTodos)
            {
                await CargarPrsPendientesTodosAsync(prActives, reposActivos, reposOld);
                filtroSql = null;
                repoEntornos = reposActivos[0];
                modelRepositoryId = FiltroAplicativoTodos;
            }
            else
            {
                var repo = repoConcreto!;
                await AgregarPrsPendientesAsync(prActives, repo.ReleaseDefinitionId, AzureProjectType.Default);
                var repoOld = reposOld.FirstOrDefault(x => x.RepoName == repositoryId);
                if (repoOld != null)
                    await AgregarPrsPendientesAsync(prActives, repoOld.ReleaseDefinitionId, AzureProjectType.ScriptOld);

                filtroSql = repo.RepoName;
                repoEntornos = repo;
                modelRepositoryId = repo.RepoName;
            }

            var prIds = prActives.Where(id => id > 0).Distinct().ToList();
            var model = await _repo.GetScripts(false, usuario, descripcion, filtroSql, page, idEstado, prIds, nroIncidente);
            CompletarModeloBusqueda(model, usuario, descripcion, modelRepositoryId, deleted: false, idEstado, nroIncidente, soloPendientes: true);
            model.Environments = await _environmentService.GetEnvironmentDtosScriptsAsync(repoEntornos.RepoName);
            return View(model);
        }


        [HttpGet]
        [Authorize(Roles = "DesarrolloTecnico, DesarrolloFuncional")]
        public async Task<IActionResult> AddNewScript()
        {
            var g = await _adGroupService.GetInfoUser();
            var validGroups = new List<string>() { "DesarrolloFuncional", "DesarrolloTecnico" };
            if (!g.Roles.Any(x => validGroups.Contains(x.GroupName)))
                return Unauthorized();

            var structures = _configuration.GetSection("SqlTemplate").Get<ICollection<StructureSQLType>>()!;
            ViewBag.Templates = structures.Where(x => x.Seleccionable).ToList();

            var aplicativos = _configuration.GetProjectSettings(false, true, null, null);
            CargarComboAplicativos(aplicativos.FirstOrDefault()!.Repos);

            var tiposCambios = await _repo.GetTiposCambios();

            ViewBag.TiposCambios = new SelectList(tiposCambios, "IdTipoCambio", "Descripcion");

            var m = new ScriptRequestViewModel();
            return View(m);
        }

        [HttpPost]
        public async Task<IActionResult> SearchStructures([FromBody] SearchStructuresRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.RepositoryId))
                {
                    return BadRequest(new { error = "Repository ID is required" });
                }

                var structures = await _azureService.SearchStructures(
                    request.RepositoryId,
                    request.StructureType,
                    request.SearchTerm
                );

                return Json(structures);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching SQL structures for repository {RepositoryId}", request.RepositoryId);
                return StatusCode(500, new { error = "Error interno del servidor", details = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetStructureContent([FromBody] GetStructureContentRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.RepositoryId) || string.IsNullOrEmpty(request.FilePath))
                {
                    return BadRequest(new { error = "Repository ID and File Path are required" });
                }

                var content = await _azureService.GetFileContentFromPathAsync(request.RepositoryId, request.FilePath);

                if (content == null)
                {
                    return NotFound(new { error = "Structure not found", path = request.FilePath });
                }

                return Content(content, "text/plain", Encoding.UTF8);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting structure content for {FilePath}", request.FilePath);
                return StatusCode(500, new { error = "Error interno del servidor", details = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CheckStructureExists([FromBody] CheckStructureRequest request)
        {
            try
            {
                var structures = await _azureService.SearchStructures(
                    request.RepositoryId,
                    request.StructureType,
                    request.StructureName
                );

                // Filtrar por coincidencia exacta
                var exists = structures.Any(s =>
                    s.Name.Equals(request.StructureName, StringComparison.OrdinalIgnoreCase) &&
                    s.Type == request.StructureType
                );

                return Ok(new { exists });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetRealNamePath([FromBody] GetStructureContentRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.RepositoryId) || string.IsNullOrEmpty(request.FilePath))
                {
                    return BadRequest(new { error = "Repository ID and File Path are required" });
                }

                var content = await _azureService.GetActualPathAzureDevOps(request.RepositoryId, request.FilePath);

                if (content == null)
                {
                    return NotFound(new { error = "Structure not found", path = request.FilePath });
                }

                return Ok(new { path = content });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting structure content for {FilePath}", request.FilePath);
                return StatusCode(500, new { error = "Error interno del servidor", details = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetContentForEdit([FromBody] GetContentForEditRequest request)
        {
            var structures = _configuration.GetSection("SqlTemplate").Get<ICollection<StructureSQLType>>()!;
            request.Type = request.Type.Replace("Table", "AlterTable");
            var structure = structures.FirstOrDefault(x => String.Compare(x.Type, request.Type, StringComparison.OrdinalIgnoreCase) == 0);
            TemplateSqlManager templateSqlManager = new TemplateSqlManager(structure!);
            var r = await templateSqlManager.GetContentTemplate(request.Schema, request.Name);
            return Json(r);
        }

        [HttpGet]
        public async Task<IActionResult> GetTemplate(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    return BadRequest(new { error = "Repository ID and File Path are required" });
                }
                var content = await TemplateSqlManager.GetTemplateContentAsync(filePath);

                if (content == null)
                {
                    return NotFound(new { error = "Structure not found", path = filePath });
                }

                return Content(content, "text/plain", Encoding.UTF8);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting structure content for {FilePath}", filePath);
                return StatusCode(500, new { error = "Error interno del servidor", details = ex.Message });
            }
        }

        [HttpPost]
        [Authorize(Roles = "DesarrolloTecnico, DesarrolloFuncional")]
        public async Task<IActionResult> AddNewScript(ScriptRequestViewModel request)
        {
            if (ModelState.IsValid)
            {
                await ProcesarScript(request, generarPR: request.GeneratePR, true);
            }
            return RedirectToAction("Index");
        }

        private string ArmarOrden(List<VirtualFileViewModel> files)
        {
            string order = "";
            foreach (var f in files)
            {
                if (f.Estado != "eliminado")
                    order += f.Name + Environment.NewLine;
            }
            return order;
        }

        private async Task<Script?> ProcesarScript(ScriptRequestViewModel request, bool generarPR, bool firstCommit)
        {

            request.FolderAndBranchNameDesa = Guid.NewGuid().ToString();
            _azureService.ForceProyectType(request.AzureProjectType.ToAzureProjectType());

            if (!await _azureService.CreateBranchAsync(request.RepositoryId, request.FolderAndBranchNameDesa))
            {
                await _tempDataHelper.Message(TypeNotify.Error, "Imposible crear la rama para subir archivos...", "Error");
                return null;
            }

            int? idPr = null;

            var archivos = JsonSerializer.Deserialize<List<VirtualFileViewModel>>(request.JsonVirtualFiles);
            if (generarPR) // si libera, va el execution_order sino no lo agrego 
            {
                archivos!.Add(new VirtualFileViewModel()
                {
                    Content = ArmarOrden(archivos!),
                    Estado = "nuevo",
                    Name = "execution_order.txt",
                    EsBinario = false,
                    Origin = "template",
                });
            }
            var estados = new[] { "nuevo", "modificado" };
            if (archivos!.Any(x => estados.Contains(x.Estado)))
            {
                try
                {
                    await _azureService.PushFiles(request.RepositoryId, request.FolderAndBranchNameDesa, archivos!, firstCommit);
                }
                catch (Exception ex)
                {
                    await _tempDataHelper.Message(TypeNotify.Success, $"Error al subir archivo. {ex.Message}", "Cambios");
                    return null;
                }
            }

            if (generarPR)
            {
                List<string> revIds = [];
                var iycRv = _configuration.GetReviewerConfig("DBA");
                revIds.Add(iycRv.FirstOrDefault()!.Id);
                var labels = await ArmarLabelsEnvioAsync(request.IsHotfix, request.Tipo, request.Numero);
                idPr = await _azureService.GeneratePullRequest(
                    request.RepositoryId,
                    request.Descripcion,
                    request.Descripcion,
                    request.FolderAndBranchNameDesa,
                    _configuration["AzureSettings:BranchPR"]!,
                    true,
                    labels);

                if (bool.Parse(_configuration["Testing:IncludeReviewersScripts"]!))
                {
                    await _azureService.AddReviewerToPR(request.RepositoryId, idPr.Value, revIds.ToArray());

                }
                if (bool.Parse(_configuration["Testing:AutocompleteScripts"]!))
                {
                    await _azureService.EnableAutoCompleteForPR(request.RepositoryId, idPr ?? 0);
                }

                request.PullRequestIdQA = idPr;
            }

            var sg = await _adGroupService.GetInfoUser();
            request.Usuario = sg!.Username;
            var res = await _repo.AddPasaje(request);

            if (res != null)
            {
                var msgPr = idPr != null ? $"PullRequest: {idPr}" : "";
                await NotificarGuardadoAsync($"Cambios guardados correctamente. {msgPr}");
                // mailing
                await SendMail(idPr, request, res);
            }
            else
            {
                await _tempDataHelper.Message(TypeNotify.Error, "Error al guardar scripts", "Error");
            }

            return res;
        }

        private async Task SendMail(int? idPr, ScriptRequestViewModel request, Script res)
        {
            if (idPr.HasValue && idPr.Value != 0)
            {
                var dbaConfig = _configuration.GetReviewerConfig("DBA");
                var url = Url.Action("Vote", "SqlFiles", new { IdCambio = res.IdScript }, protocol: Request.Scheme);
                var title = $"ID {res.IdScript}-{res.IdTipoCambioNavigation.Descripcion} {res.NroIncidente}";
                var additional_data = new Dictionary<string, string>
                    {
                        { "CreatedBy", request.Usuario! },
                        { "CreationDate", DateTime.Now.ToString("dd/MM/yyyy HH:mm") },
                        { "SourceBranch", request.FolderAndBranchNameDesa! },
                        { "TargetBranch", _configuration["AzureSettings:BranchPR"]! },
                        { "Description", request.Descripcion },
                        { "PasajeId", res.IdScript.ToString() },
                        { "TipoCambio", res.IdTipoCambioNavigation.Descripcion },
                        { "Incidente", res.NroIncidente.ToString() },
                        { "Hotfix", request.IsHotfix ? "Sí" : "No" }
                    };
                await _emailService.SendPullRequestNotificationDBAAsync("DBA", dbaConfig.FirstOrDefault()!.Email!, idPr.Value.ToString(), title, request.RepositoryId, url!, additional_data);
            }
        }
        private async Task<bool> ProcesarEditScript(ScriptRequestViewModel request, bool generarPR)
        {

            int? idPr = null;
            _azureService.ForceProyectType(request.AzureProjectType.ToAzureProjectType());

            var archivos = JsonSerializer.Deserialize<List<VirtualFileViewModel>>(request.JsonVirtualFiles)!;
            if (request.IdScript is > 0)
            {
                var persistido = await _repo.ScriptToRequestViewModel(request.IdScript.Value);
                ReconciliarEstadosConPersistido(archivos, persistido?.JsonVirtualFiles);
            }
            if (generarPR) // si libera, va el execution_order sino no lo agrego 
            {
                archivos!.Add(new VirtualFileViewModel()
                {
                    Content = ArmarOrden(archivos!),
                    Estado = "nuevo",
                    Name = "execution_order.txt",
                    EsBinario = false,
                    Origin = "template",
                });
            }
            var estados = new[] { "modificado", "nuevo", "eliminado" };
            var estadosValidos = new[] { "modificado", "nuevo", "eliminado", "sinCambios" };
            var estadosDesconocidos = archivos
                .Select(x => x.Estado)
                .Where(e => !estadosValidos.Contains(e))
                .Distinct()
                .ToList();
            if (estadosDesconocidos.Count > 0)
            {
                _logger.LogWarning("Estados de archivo no reconocidos para script {IdScript}: {Estados}",
                    request.IdScript, string.Join(", ", estadosDesconocidos));
            }
            if (archivos.Any(x => estados.Contains(x.Estado)))
            {
                var cleanListAzure = archivos.Where(x => estados.Contains(x.Estado)).ToList();
                try
                {
                    await _azureService.PushFiles(request.RepositoryId, request.FolderAndBranchNameDesa!, cleanListAzure, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al subir archivos modificados para el script {IdScript}", request.IdScript);
                }
            }
            else if (!generarPR)
            {
                await _tempDataHelper.Message(TypeNotify.Warning, "No se han detectados cambios para guardar", "Info");
                return true;
            }

            if (generarPR)
            {
                List<string> revIds = [];
                var iycRv = _configuration.GetReviewerConfig("DBA");
                revIds.Add(iycRv.FirstOrDefault()!.Id);
                _azureService.ForceProyectType(request.AzureProjectType.ToAzureProjectType());
                var labels = await ArmarLabelsEnvioAsync(request.IsHotfix, request.Tipo, request.Numero);
                idPr = await _azureService.GeneratePullRequest(
                    request.RepositoryId,
                    request.Descripcion,
                    request.Descripcion,
                    request.FolderAndBranchNameDesa!,
                    _configuration["AzureSettings:BranchPR"]!,
                    true,
                    labels);

                if (bool.Parse(_configuration["Testing:IncludeReviewersScripts"]!))
                {
                    await _azureService.AddReviewerToPR(request.RepositoryId, idPr.Value, revIds.ToArray());

                }
                if (bool.Parse(_configuration["Testing:AutocompleteScripts"]!))
                {
                    await _azureService.EnableAutoCompleteForPR(request.RepositoryId, idPr ?? 0);
                }

                request.PullRequestIdQA = idPr;
            }
            var cleanList = archivos.Where(x => x.Estado != "eliminado").ToList();
            request.JsonVirtualFiles = JsonSerializer.Serialize(cleanList);
            var res = await _repo.UpdatePasaje(request);

            if (res != null)
            {
                var msgPr = idPr != null ? $"PullRequest: {idPr}" : "";
                await NotificarGuardadoAsync($"Cambios guardados correctamente. {msgPr}");
                // mailing
                if (idPr.HasValue && idPr.Value != 0)
                {
                    var DBAGroup = await _dbContext.AdGroupMappings.Where(x => x.GroupName == "DBA").Select(x => x.Email).FirstOrDefaultAsync();
                    var url = Url.Action("Vote", "SqlFiles", new { IdCambio = res.IdScript }, protocol: Request.Scheme);
                    var title = $"ID {res.IdScript}-{res.IdTipoCambioNavigation.Descripcion} {res.NroIncidente}";
                    var additional_data = new Dictionary<string, string>
                    {
                        { "CreatedBy", res.Usuario },
                        { "CreationDate", DateTime.Now.ToString("dd/MM/yyyy HH:mm") },
                        { "SourceBranch", request.FolderAndBranchNameDesa! },
                        { "TargetBranch", _configuration["AzureSettings:BranchPR"]! },
                        { "Description", request.Descripcion },
                        { "PasajeId", res.IdScript.ToString() },
                        { "TipoCambio", res.IdTipoCambioNavigation.Descripcion },
                        { "Incidente", res.NroIncidente.ToString() },
                        { "Hotfix", request.IsHotfix ? "Sí" : "No" }
                    };
                    var prDetail = idPr.HasValue ? idPr.Value.ToString() : "N/A";
                    await _emailService.SendPullRequestNotificationDBAAsync("DBA", DBAGroup!, prDetail, title, request.RepositoryId, url!, additional_data);
                }
            }
            else
            {
                await _tempDataHelper.Message(TypeNotify.Error, "Error al guardar scripts", "Error");
            }



            return true;
        }

        /// <summary>
        /// El cliente puede enviar _estado "sinCambios" aunque el contenido SQL haya cambiado
        /// (JS cacheado o el modal no marcó modificado). La fuente de verdad es el JSON persistido.
        /// </summary>
        private static void ReconciliarEstadosConPersistido(List<VirtualFileViewModel> archivos, string? jsonPersistido)
        {
            var estadosCambio = new[] { "modificado", "nuevo", "eliminado" };
            var persistidos = new List<VirtualFileViewModel>();
            if (!string.IsNullOrWhiteSpace(jsonPersistido))
            {
                persistidos = JsonSerializer.Deserialize<List<VirtualFileViewModel>>(jsonPersistido) ?? [];
            }

            foreach (var archivo in archivos)
            {
                if (estadosCambio.Contains(archivo.Estado))
                    continue;

                var previo = persistidos.FirstOrDefault(p => NombresArchivoEquivalentes(p.Name, archivo.Name));
                if (previo is null)
                {
                    archivo.Estado = "nuevo";
                    continue;
                }

                if (!string.Equals(NormalizarContenidoSql(previo.Content), NormalizarContenidoSql(archivo.Content), StringComparison.Ordinal)
                    || !string.Equals(previo.Name, archivo.Name, StringComparison.Ordinal))
                {
                    archivo.Estado = "modificado";
                }
            }
        }

        private static string NormalizarContenidoSql(string? contenido) =>
            (contenido ?? "").Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

        private static bool NombresArchivoEquivalentes(string? a, string? b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                return false;
            if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
                return true;

            static string Normalizar(string n) => n.Trim().TrimStart('/').Replace('\\', '/');
            return string.Equals(Normalizar(a), Normalizar(b), StringComparison.OrdinalIgnoreCase);
        }

        [HttpGet]
        [Authorize(Roles = "DesarrolloTecnico, DesarrolloFuncional")]
        public async Task<IActionResult> Edit(int idCambio)
        {
            var g = await _adGroupService.GetInfoUser();

            var model = await _repo.ScriptToRequestViewModel(idCambio);

            var usr = await _adGroupService.GetInfoUser();
            if (model.Usuario != usr.Username)
            {
                await _tempDataHelper.Message(TypeNotify.Error, "No se puede editar un cambio de otro analista", "Error con status code 401");
                return RedirectToAction("Index");
            }

            var structures = _configuration.GetSection("SqlTemplate").Get<ICollection<StructureSQLType>>()!;
            ViewBag.Templates = structures.Where(x => x.Seleccionable).ToList();

            var tiposCambios = await _repo.GetTiposCambios();
            ViewBag.TiposCambios = new SelectList(tiposCambios, "IdTipoCambio", "Descripcion");

            var aplicativos = _configuration.GetProjectSettings(false, true, null, null);
            CargarComboAplicativos(aplicativos.FirstOrDefault()!.Repos);

            var archivos = JsonSerializer.Deserialize<List<VirtualFileViewModel>>(model.JsonVirtualFiles)!;

            foreach (var file in archivos)
            {
                model.Files.Add(new FileView()
                {
                    FileName = file.Name,
                    TemplateType = file.TemplateType,
                    IdFileAzure = file.Name,
                    Content = file.Content,
                    Path = file.Origin != "devops" ? model.FolderAndBranchNameDesa! : Path.GetDirectoryName(file.Name)!.Replace("\\", "/"),
                    Origin = file.Origin,
                });
            }

            return View(model);
        }
        [HttpPost]
        [Authorize(Roles = "DesarrolloTecnico, DesarrolloFuncional")]
        public async Task<IActionResult> EditScript(ScriptRequestViewModel request)
        {
            if (ModelState.IsValid)
            {
                await ProcesarEditScript(request, generarPR: request.GeneratePR);
            }
            return RedirectToAction("Index");
        }

        private async Task<ContentFileView> GetViewModel(int idCambio)
        {
            var res = await _repo.ScriptToViewModel(idCambio);
            
            if (res.PullRequestIdQA.HasValue && res.PullRequestIdQA.Value > 0)
            {
                _azureService.ForceProyectType(res.AzureProjectType.ToAzureProjectType());
                res.PullRequest = await _azureService.GetPullRequestAsync(res.PullRequestIdQA.Value);
            }

            var archivos = JsonSerializer.Deserialize<List<VirtualFileViewModel>>(res.FilesJson);
            res.Files.Clear();

            foreach (var item in archivos!)
            {
                var parsed = SqlParser.ParseStructureFromContent(item.Content);
                var pathDevops = !item.Name.Split('/').Any() ? item.Name : $"/{res.BranchNameDesa}/{item.Name}";
                res.Files.Add(new FileView()
                {
                    Content = item.Content,
                    FileName = item.Name,
                    IdFileAzure = item.RutaOriginal!,
                    Origin = item.Origin,
                    ParsedStructure = parsed,
                    Path = pathDevops
                });
            }
            return res;
        }

        [HttpGet]
        [Authorize(Roles = "DBA")]

        public async Task<IActionResult> Vote(int idCambio)
        {
            var res = await GetViewModel(idCambio);
            return View(res);
        }

        [HttpGet]
        public async Task<IActionResult> ViewFiles(int idCambio)
        {
            var res = await GetViewModel(idCambio);

            return View(res);
        }


        [HttpPost]
        [Authorize(Roles = "DBA")]
        public async Task<IActionResult> Vote(ContentFileView model)
        {
            var log = await _adGroupService.GetInfoUser();
            //obtener el pat
            var pat = await _patRepositoryService.GetPatByUser(log.Username);
            var prId = (model.PullRequestIdQA.HasValue) ? model.PullRequestIdQA.Value : 0;

            try
            {
                _azureService.ForceProyectType(model.AzureProjectType.ToAzureProjectType());
                await _azureService.VoteOnPullRequestWithPatAsync(pat!, log.Username, model.RepositoryId, prId, model.Vote, model.Comment);
                if (model.Vote < 0)
                {
                    //rechazo. Abandono PR y guardo en la db
                    await _azureService.AbandonarPullRequestAsync(model.RepositoryId, prId);
                    
                }
                await _tempDataHelper.Message(TypeNotify.Success);

            }
            catch (Exception ex)
            {
                await _tempDataHelper.Message(TypeNotify.Error, ex.Message, "Error al intentar votar PR");

            }
            return RedirectToAction("Index", routeValues: new { repositoryId = FiltroAplicativoTodos }, controllerName: "SqlFiles");

        }

        [RequireValidPat]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DesarrolloTecnico, DesarrolloFuncional")]
        public async Task<IActionResult> Delete([FromBody] int id)
        {
            var script = await _repo.ScriptToViewModel(id);
            var r = await _repo.DeleteScript(id);
            if (r)
            {
                _azureService.ForceProyectType(script.AzureProjectType.ToAzureProjectType());
                await _azureService.DeleteBranchAsync(script.RepositoryId, script.BranchNameDesa);
                await _tempDataHelper.Message(TypeNotify.Success);
                return Json(new
                {
                    success = true,
                    message = "Eliminado correctamente",
                    redirectUrl = Url.Action("Index", "SqlFiles")
                });
            }
            else
            {
                await _tempDataHelper.Message(TypeNotify.Error, "No se pudo eliminar el registro seleccionado");
                return Json(new
                {
                    success = false,
                    message = "No se pudo eliminar el registro seleccionado",
                    redirectUrl = Url.Action("Index", "SqlFiles")
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetLastCommitInfo(string repositoryId, string path)
        {
            var commit = await _azureService.GetLastModifiedAsync(repositoryId, path);

            if (commit == null)
            {
                return Json(new { ok = false });
            }

            return Json(new
            {
                ok = true,
                author = commit.Author.Name,
                email = commit.Author.Email,
                date = commit.Author.Date
            });
        }

        [RequireValidPat]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DesarrolloTecnico, DesarrolloFuncional")]
        public async Task<IActionResult> Clonar([FromBody] int id)
        {
            if (id <= 0)
                return Json(new { success = false, message = "Identificador de envío inválido." });

            try
            {
                var original = await _repo.GetScriptById(id);
                if (original == null)
                    return Json(new { success = false, message = "No se encontró el envío a clonar." });

                // execution_order.txt lo regenera la liberación, no se copia del original
                var archivos = ClonacionEnviosHelper.NormalizarParaClon(original.Files, "execution_order.txt");
                if (archivos.Count == 0)
                    return Json(new { success = false, message = "El envío original no tiene archivos para clonar." });

                var clon = new ScriptRequestViewModel
                {
                    Descripcion = original.Descripcion ?? string.Empty,
                    RepositoryId = original.Aplicacion,
                    AzureProjectType = original.AzureProjectType,
                    Numero = original.NroIncidente,
                    Tipo = original.IdTipoCambio,
                    CommitId = original.CommitId,
                    IsHotfix = original.IsHotfix,
                    TareasJson = original.TareasJson,
                    JsonVirtualFiles = JsonSerializer.Serialize(archivos),
                    GeneratePR = false
                };

                var nuevo = await ProcesarScript(clon, generarPR: false, firstCommit: true);
                if (nuevo == null)
                    return Json(new { success = false, message = "No se pudo clonar el envío." });

                _logger.LogInformation("Script {IdOriginal} clonado como {IdClon} por {User}", id, nuevo.IdScript, nuevo.Usuario);

                return Json(new
                {
                    success = true,
                    id = nuevo.IdScript,
                    redirectUrl = Url.Action("Edit", "SqlFiles", new { idCambio = nuevo.IdScript })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al clonar script {Id}", id);
                return Json(new { success = false, message = "Error interno al clonar el envío." });
            }
        }

        [RequireValidPat]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DesarrolloTecnico, DesarrolloFuncional")]
        public async Task<IActionResult> Descartar([FromBody] int id)
        {
            try
            {
                var script = await _repo.ScriptToViewModel(id);
                if (script == null)
                    return Json(new { success = false, message = "Script no encontrado." });

                var usr = await _adGroupService.GetInfoUser();
                if (script.Usuario != usr.Username)
                {
                    _logger.LogWarning("Usuario {User} intentó descartar script {Id} de {Owner}", usr.Username, id, script.Usuario);
                    return Json(new { success = false, message = "Solo el creador puede anular este envío." });
                }

                if (!script.PullRequestIdQA.HasValue || script.PullRequestIdQA.Value == 0)
                {
                    await _repo.Descartar(id, Helpers.Constants.Tables.Scripts, true);
                    await _tempDataHelper.Message(TypeNotify.Success, "Script anulado correctamente.");
                    return Json(new { success = true });
                }

                _azureService.ForceProyectType(script.AzureProjectType.ToAzureProjectType());

                var enProd = await _azureService.HasDeployedInProdAsync(script.PullRequestIdQA.Value);
                if (enProd)
                {
                    return Json(new
                    {
                        success = false,
                        message = "No se puede anular porque el envío ya fue aplicado en Producción."
                    });
                }

                var azureOk = await _azureService.CancelarDeployActivoNoProdAsync(script.PullRequestIdQA.Value, _logger);

                var discarded = await _repo.Descartar(id, Helpers.Constants.Tables.Scripts, true);
                if (!discarded)
                {
                    return Json(new { success = false, message = "No se pudo anular el script." });
                }

                await _azureService.AbandonarPullRequestAsync(script.RepositoryId, script.PullRequestIdQA.Value);

                if (azureOk)
                    await _tempDataHelper.Message(TypeNotify.Success, "Script anulado correctamente.");
                return Json(new
                {
                    success = true,
                    warning = azureOk ? null : AzureServiceExtensions.WarningReleaseNoCancelado
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al descartar script {Id}", id);
                return Json(new { success = false, message = "Error interno al anular el script." });
            }
        }

        private async Task<IReadOnlyList<string>> ArmarLabelsEnvioAsync(bool isHotfix, int idTipoCambio, int nroIncidente)
        {
            var tipos = await _repo.GetTiposCambios();
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

        private static bool EsFiltroTodos(string? repositoryId)
        {
            if (string.IsNullOrWhiteSpace(repositoryId))
                return true;

            return string.Equals(repositoryId, FiltroAplicativoTodos, StringComparison.OrdinalIgnoreCase);
        }

        private IReadOnlyList<RepoSetting> ObtenerReposScriptsActivos()
        {
            var aplicativos = _configuration.GetProjectSettings(false, true, null, null);
            return aplicativos.FirstOrDefault()?.Repos ?? [];
        }

        private (IReadOnlyList<RepoSetting> Activos, IReadOnlyList<RepoSetting> Old) ObtenerReposScriptsActivosYOld()
        {
            var proyectos = _configuration.GetProjectSettings(false, null, null, null);
            var reposActivos = proyectos.FirstOrDefault(x => x.Active)?.Repos ?? [];
            var reposOld = proyectos.FirstOrDefault(x => !x.Active)?.Repos ?? [];
            return (reposActivos, reposOld);
        }

        private void CargarComboAplicativos(IEnumerable<RepoSetting> repos)
        {
            var ordenados = repos.OrderBy(r => r.DisplayName, StringComparer.CurrentCultureIgnoreCase);
            ViewBag.Aplicativos = new SelectList(ordenados, "RepoName", "DisplayName");
        }

        private (string? FiltroSql, RepoSetting RepoEntornos, string ModelRepositoryId) ResolverFiltroAplicativo(
            string? repositoryId,
            IReadOnlyList<RepoSetting> reposActivos)
        {
            var primero = reposActivos[0];

            if (EsFiltroTodos(repositoryId))
                return (null, primero, FiltroAplicativoTodos);

            var repo = reposActivos.FirstOrDefault(x => x.RepoName == repositoryId);
            if (repo is null)
            {
                _logger.LogWarning("Aplicativo desconocido {RepositoryId}; se usa el filtro TODOS", repositoryId);
                return (null, primero, FiltroAplicativoTodos);
            }

            return (repo.RepoName, repo, repo.RepoName);
        }

        private void CompletarModeloBusqueda(
            SearchScripts model,
            string? usuario,
            string? descripcion,
            string? repositoryId,
            bool deleted,
            int idEstado,
            int? nroIncidente,
            bool soloPendientes)
        {
            model.Deleted = deleted;
            model.RepositoryId = repositoryId;
            model.Usuario = usuario;
            model.Descripcion = descripcion;
            model.IdEstado = idEstado;
            model.NroIncidente = nroIncidente;
            model.SoloPendientes = soloPendientes;
            model.StageBuckets = ObtenerStageBuckets();
        }

        private IReadOnlyList<InformeGerencialStageBucket> ObtenerStageBuckets()
        {
            var buckets = _configuration.GetSection("InformeGerencial:Stages")
                .Get<List<InformeGerencialStageBucket>>() ?? [];

            var valid = buckets
                .Where(b => !string.IsNullOrWhiteSpace(b.Key))
                .Select(b =>
                {
                    b.Key = b.Key.Trim();
                    b.Display = string.IsNullOrWhiteSpace(b.Display) ? b.Key : b.Display.Trim();
                    b.Nombres = b.Nombres
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Select(n => n.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    return b;
                })
                .ToList();

            if (valid.Count == 0)
                _logger.LogWarning("InformeGerencial:Stages no está configurado o no tiene buckets válidos");

            return valid;
        }

        private async Task CargarPrsPendientesTodosAsync(
            List<int> destino,
            IReadOnlyList<RepoSetting> reposActivos,
            IReadOnlyList<RepoSetting> reposOld)
        {
            foreach (var definitionId in reposActivos.Select(r => r.ReleaseDefinitionId).Where(id => id > 0).Distinct())
                await AgregarPrsPendientesAsync(destino, definitionId, AzureProjectType.Default);

            foreach (var definitionId in reposOld.Select(r => r.ReleaseDefinitionId).Where(id => id > 0).Distinct())
                await AgregarPrsPendientesAsync(destino, definitionId, AzureProjectType.ScriptOld);
        }

        private async Task AgregarPrsPendientesAsync(List<int> destino, int definitionId, AzureProjectType tipo)
        {
            if (definitionId <= 0)
                return;

            try
            {
                _azureService.ForceProyectType(tipo);
                var releases = await _azureService.GetPendingReleasesWithPullRequestInfo(definitionId, EstadosReleasePendientes);
                if (releases is null)
                    return;

                destino.AddRange(releases
                    .Select(x => x.PullRequestId)
                    .Where(id => id is > 0)
                    .Select(id => id!.Value));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo releases pendientes para definition {DefinitionId} en proyecto {AzureProjectType}", definitionId, tipo);
            }
        }
    }
}
