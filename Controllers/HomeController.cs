using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PasadorScript.DbModels;
using PasadorScript.DTO.AzureDev;
using PasadorScript.Extensions;
using PasadorScript.Models;
using PasadorScript.Repository;
using PasadorScript.Services.Azure;
using PasadorScript.Services.Crypto;
using PasadorScript.ViewModels;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PasadorScript.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly IDbRepository _dbRepository;
        private readonly IAzurePullRequestService _azurePullRequestService;
        private readonly IAzureAnalyticService _azureAnalyticService;
        private readonly IAzureService _azureService;

        public HomeController(ILogger<HomeController> logger, IWebHostEnvironment env, IConfiguration configuration,
            IDbRepository dbRepository, IAzureAnalyticService azureAnalyticService, IAzurePullRequestService azurePullRequestService,
            IAzureService azureService)
        {
            _logger = logger;
            _environment = env;
            _configuration = configuration;
            _dbRepository = dbRepository;
            _azurePullRequestService = azurePullRequestService;
            _azureAnalyticService = azureAnalyticService;
            _azureService = azureService;
        }

        public async Task<IActionResult> Index(
            string? statusPrGx = null,
            string? statusPrScripts = null,
            int? definitionId = null,
            string? definitionIdScripts = null)
        {
            

            // Labels para filtros
            var statusLabelGx = statusPrGx switch
            {
                "active" => "Pendientes de release",
                "completed" => "En release",
                "abandoned" => "Rechazados",
                _ => "Todos"
            };

            var statusLabelScripts = statusPrScripts switch
            {
                "active" => "Pendientes",
                "completed" => "Completados",
                "abandoned" => "Fallidos",
                _ => "Todos"
            };
                      

            // ============ Pipelines Config ============
            var pipelinesConfig = _configuration.GetSection("Projects").Get<List<ProjectSetting>>();
            var reposGx = pipelinesConfig.Where(x => x.IsGx && x.Active).SelectMany(x => x.Repos).ToList();
            var defaultBuild = pipelinesConfig.First(x => x.IsGx && x.Active && x.DefaultReleaseDefinitionId.HasValue)?.DefaultReleaseDefinitionId ?? 0;

            // GeneXus Pipelines
            ViewBag.Pipelines = new List<SelectListItem>();
            var selectedPipelineGx = reposGx.FirstOrDefault(x=> x.ReleaseDefinitionId == (definitionId ?? defaultBuild));
            foreach (var p in reposGx!)
            {
                ViewBag.Pipelines.Add(new SelectListItem
                {
                    Text = p.PipelineName,
                    Value = p.ReleaseDefinitionId.ToString()
                });
            }
            ViewBag.SelectedId = (definitionId ?? defaultBuild);

            // Scripts Pipelines
            var aplicativos = _configuration.GetProjectSettings(false, true, null, null);
            // Repos para scripts
            RepoSetting? repo = null;
            if (definitionIdScripts == null)
            {
                repo = aplicativos.FirstOrDefault()!.Repos!.FirstOrDefault();
            }
            else
            {
                repo = aplicativos.FirstOrDefault()!.Repos!.FirstOrDefault(x => x.ReleaseDefinitionId.ToString() == definitionIdScripts);
            }
            ViewBag.PipelinesScripts = new List<SelectListItem>();
            foreach (var p in aplicativos.FirstOrDefault()!.Repos)
            {
                ViewBag.PipelinesScripts.Add(new SelectListItem
                {
                    Text = p.DisplayName,
                    Value = p.RepoName
                });
            }
            ViewBag.SelectedIdScripts = definitionIdScripts ?? repo!.RepoName;

            // ============ GeneXus PRs ============
            List<AzureDevopsPullRequest> prGxPendings;
            List<OrdenBuildViewModel> modelsGx = new();

            _azurePullRequestService.SetProjectType(AzureProjectType.GeneXus);

            if (statusPrGx == null)
            {
                prGxPendings = await _azurePullRequestService.GetPullRequestsAsync(
                    selectedPipelineGx?.RepoName!, null);
                prGxPendings.AddRange(await _azurePullRequestService.GetPullRequestsAsync(
                    selectedPipelineGx?.RepoName!, "completed"));
                prGxPendings.AddRange(await _azurePullRequestService.GetPullRequestsAsync(
                    selectedPipelineGx?.RepoName!, "abandoned"));
                prGxPendings = prGxPendings.OrderByDescending(x => x.CreationDate).ToList();
            }
            else
            {
                prGxPendings = await _azurePullRequestService.GetPullRequestsAsync(
                    selectedPipelineGx?.RepoName!, statusPrGx);
            }

            foreach (var prGx in prGxPendings)
            {
                var b = await _dbRepository.GetBuildByIdPullRequest(prGx.PullRequestId);
                modelsGx.Add(new OrdenBuildViewModel()
                {
                    BuildModel = b == null ? null : new BuildGxViewWithClasses(b),
                    PullRequest = prGx
                });
            }

            // ============ Scripts PRs ============
            List<AzureDevopsPullRequest> prScriptsPendings;
            List<OrdenBuildViewModel> modelsScripts = new();

            _azurePullRequestService.SetProjectType(AzureProjectType.Default);

            if (statusPrScripts == null)
            {
                prScriptsPendings = await _azurePullRequestService.GetPullRequestsAsync(
                    repo.RepoName, null);
                prScriptsPendings.AddRange(await _azurePullRequestService.GetPullRequestsAsync(
                    repo.RepoName, "active"));
                prScriptsPendings.AddRange(await _azurePullRequestService.GetPullRequestsAsync(
                    repo.RepoName, "completed"));
                prScriptsPendings.AddRange(await _azurePullRequestService.GetPullRequestsAsync(
                    repo.RepoName, "abandoned"));
                prScriptsPendings = prScriptsPendings.OrderByDescending(x => x.CreationDate).ToList();
            }
            else
            {
                prScriptsPendings = await _azurePullRequestService.GetPullRequestsAsync(
                    repo.RepoName, statusPrScripts);
            }

            foreach (var prScript in prScriptsPendings)
            {
                modelsScripts.Add(new OrdenBuildViewModel()
                {
                    PullRequest = prScript
                });
            }

            // ============ Analytics ============
            _azureAnalyticService.SetProjectType(AzureProjectType.GeneXus);
            var analyticsGx = await _azureAnalyticService.GetPipelineAnalyticsAsync(
                definitionId ?? defaultBuild);

            _azureAnalyticService.SetProjectType(AzureProjectType.Default);
            var definitionIdScript = await _azureService.GetDefinitionIdByBuildName(definitionIdScripts ?? repo.RepoName!);
            var analyticsScripts = await _azureAnalyticService.GetPipelineAnalyticsAsync(definitionIdScript);

            // ============ Agent Pools ============
            var agents = await _azureAnalyticService.GetAgentPoolsAsync();

            var model = new DashBoardViewModel()
            {
                PullRequestGx = modelsGx,
                PullRequestScripts = modelsScripts,
                StatusFilterLabel = statusLabelGx,
                StatusFilterLabelScripts = statusLabelScripts,
                GxAnalytics = analyticsGx,
                ScriptsAnalytics = analyticsScripts,
                AgentPools = agents
            };

            return View(model);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}