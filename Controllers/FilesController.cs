using Azure.Core;
using Microsoft.AspNetCore.Mvc;
using PasadorScript.DTO.AzureDev;
using PasadorScript.DTO.TeamDev;
using PasadorScript.Helpers;
using PasadorScript.Repository;
using PasadorScript.Services.AdGroups;
using PasadorScript.Services.Azure;
using PasadorScript.Services.Pat;
using PasadorScript.Validators;
using PasadorScript.ViewModels;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace PasadorScript.Controllers
{
    public class FilesController : Controller
    {
        private readonly IDbRepository _dbRepository;
        private readonly IAzureService _serviciosAzure;
        private readonly IConfiguration _configuration;
        private readonly IAdGroupService _adGroupService;
        private readonly TempDataHelper _tempDataHelper;
        private readonly IPatRepositoryService _patRepositoryService;

        public FilesController(IDbRepository dbRepository, IAzureService serviciosAzure, IConfiguration configuration,
            IAdGroupService adGroupService, TempDataHelper tempDataHelper, IPatRepositoryService patRepositoryService)
        {
            _dbRepository = dbRepository;
            _serviciosAzure = serviciosAzure;
            _configuration = configuration;
            _adGroupService = adGroupService;
            _tempDataHelper = tempDataHelper;
            _patRepositoryService = patRepositoryService;
        }

        [HttpGet]
        public async Task<IActionResult> ViewFiles(int idPasaje)
        {
            var res = await _dbRepository.ScriptToViewModel(idPasaje);
            if (res== null)
                return NotFound();

            // Primero deserializar el string externo
            string? jsonArray = JsonSerializer.Deserialize<string>(res?.FilesJson!);

            var list = string.IsNullOrWhiteSpace(jsonArray)
               ? new List<VirtualFileViewModel>()
               : JsonSerializer.Deserialize<List<VirtualFileViewModel>>(jsonArray);
            res.Files.Clear();
            foreach (var item in list!)
            {
                res.Files.Add(new FileView()
                {
                    Content = item.Content,
                    FileName = item.Name,
                    //IdFileAzure = item.Path,
                    //FechaCreacion = item.FechaModificacion.Value
                });
            }
            return View(res);
        }

        [HttpGet]
        [RequireValidPat]
        public async Task<IActionResult> Index(int idPasaje)
        {
            var res = await _dbRepository.ScriptToViewModel(idPasaje);
            var branchSource = res.BranchNameDesa;
            
            if (res.PullRequestIdPR.HasValue)
            {
                var prPR = await _serviciosAzure.GetPullRequestAsync(res.PullRequestIdPR.Value);
                if (prPR != null)
                {
                    if (prPR.Status == "completed")
                        branchSource = _configuration["AzureSettings:BranchPR"];
                    else
                        branchSource = _configuration["AzureSettings:BranchQA"];
                }
            }
            else
            {
                if (res.PullRequestIdQA.HasValue)
                {
                    var prPR = await _serviciosAzure.GetPullRequestAsync(res.PullRequestIdQA.Value);
                    if (prPR != null)
                    {
                        if (prPR.Status == "completed")
                            branchSource = _configuration["AzureSettings:BranchQA"];
                    }
                }
            }
            var listFiles = await _serviciosAzure.GetArchivosEnCarpeta(res.RepositoryId, res.BranchNameDesa, branchSource);
            res.Files.Clear();
            foreach (var item in listFiles)
            {
                res.Files.Add(new FileView()
                {
                    Content = await _serviciosAzure.GetFileContentAsync(res.RepositoryId, item.Path, true, branchSource),
                    FileName = item.FileName,
                    IdFileAzure = item.Path,
                    FechaCreacion = item.FechaModificacion!.Value
                });
            }
            //foreach (var item in res.Files)
            //{
            //    item.Content = await _serviciosAzure.GetFileContentAsync(res.RepositoryId, item.IdFileAzure, desdeBranchDesa, branchSource);
            //}
            return View(res);
        }

        [HttpPost]
        public async Task<IActionResult> Vote(ContentFileView model)
        {
            var log = await _adGroupService.GetInfoUser();
            //obtener el pat
            var pat = await _patRepositoryService.GetPatByUser(log.Username);
            var prId = (model.PullRequestIdQA.HasValue && !model.PullRequestIdPR.HasValue) ? model.PullRequestIdQA.Value : model.PullRequestIdPR.Value;

            try
            {
                await _serviciosAzure.VoteOnPullRequestWithPatAsync(pat!, log.Username, model.RepositoryId, prId, model.Vote, model.Comment);
                if (model.Vote < 0)
                {
                    //rechazo. Abandono PR y guardo en la db
                    await _serviciosAzure.AbandonarPullRequestAsync(model.RepositoryId, prId);
                    if (model.PullRequestIdQA.HasValue && !model.PullRequestIdPR.HasValue)
                        model.PullRequestIdQA = null;
                    else
                        model.PullRequestIdPR = null;
                    await _dbRepository.UpdatePasaje(model);
                }
                else
                {
                    //await _serviciosAzure.SetAutoCompleteAsync(model.RepositoryId, prId);
                    await _serviciosAzure.CompletePullRequestAsync(model.RepositoryId!, prId);
                }
                await _tempDataHelper.Message(TypeNotify.Success);
                
            }
            catch(Exception ex)
            {
                await _tempDataHelper.Message(TypeNotify.Error, ex.Message, "Error al intentar votar PR");
                
            }
            return RedirectToAction("Index", "Scripts", routeValues:new { RepositoryId  = model.RepositoryId });

            

        }

        [HttpGet("GetFileHistory/{repositoryId}")]
        public async Task<IActionResult> GetFileHistory(string repositoryId, 
            [FromQuery] string filePath = "sdf",
            [FromQuery] string branch = "main",
            [FromQuery] int maxResults = 50)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    return BadRequest("El parámetro filePath es requerido");
                }

                List<CommitHistory> history;

                if (!string.IsNullOrEmpty(branch) && branch != "main")
                {
                    history = await _serviciosAzure.GetFileHistoryByBranchAsync(
                        repositoryId, filePath, branch, maxResults);
                }
                else
                {
                    history = await _serviciosAzure.GetFileHistoryAsync(
                        repositoryId, filePath, maxResults);
                }

                return Ok(history);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetFileContent(string repositoryId, string filePath, string commitId = null)
        {
            try
            {
                var content = await _serviciosAzure.GetFileContentAsync(repositoryId, filePath, commitId);
                return Content(content, "text/plain");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error loading file content: {ex.Message}");
            }
        }

        [HttpGet("simulated")]
        public IActionResult GetSimulatedHistory()
        {
            // Datos de simulación para pruebas
            var simulatedHistory = new List<CommitHistory>
        {
            new CommitHistory
            {
                CommitId = "1798e8f4-d4cc-a672-b3bb-d5a7751a804a",
                Author = "You",
                Date = DateTime.Now.AddHours(-11),
                Message = "Updated query logic"
            },
            new CommitHistory
            {
                CommitId = "c2f350d6-3770-4a4e-8cea-03d2e5799f3",
                Author = "Gaffetti",
                Date = DateTime.Now.AddDays(-2),
                Message = "Initial commit"
            }
        };

            return Ok(simulatedHistory);
        }
    }
}
