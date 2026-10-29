using Microsoft.AspNetCore.Mvc.Rendering;
using PasadorScript.DTO.AzureDev;
using PasadorScript.Extensions;
using PasadorScript.Helpers;
using PasadorScript.Services.Azure;

namespace PasadorScript.Services.Environments
{
    public class EnvironmentService : IEnvironmentService
    {
        private readonly IConfiguration _configuration;
        private readonly IAzureService _azureService;

        public EnvironmentService(IConfiguration configuration, IAzureService azureService)
        {
            _configuration = configuration;
            _azureService = azureService;
        }
        public async Task<ICollection<EnvironmentDto>> GetEnvironmentDtosGxAsync(string? repositoryId = null, bool nativeBT = true)
        {
            var apps = _configuration.GetSection("Projects").Get<List<ProjectSetting>>().Where(x => x.IsGx && x.Active).FirstOrDefault();
            var aplicativos = apps.Repos.Where(x => x.OwnBuild.HasValue && x.OwnBuild.Value == nativeBT);
            RepoSetting repo = null;
            if (repositoryId == null)
            {
                repo = aplicativos?.First()!;
            }
            else
            {
                repo = aplicativos?.First(x => x.RepoName == repositoryId)!;
            }
            _azureService.ForceProyectType(AzureProjectType.GeneXus);

            var releaseDef = await _azureService.GetReleaseDefinitionsAsync();
            var defId = releaseDef.FirstOrDefault()!.Id;
            if (releaseDef != null)
            {
                var gxRepo = aplicativos.FirstOrDefault(x => x.RepoName == repo.RepoName);
                defId = gxRepo!.ReleaseDefinitionId!;
            }

            var environments = await _azureService.GetReleaseDefinitionEnvironmentsAsync(defId);
            EnvironmentHelper.UpdateProjectType(environments, AzureProjectType.GeneXus.ToProjectTypeString());

            /*olds*/
            _azureService.ForceProyectType(AzureProjectType.GenexusOld);
            var appOld = _configuration.GetSection("Projects").Get<List<ProjectSetting>>().Where(x => x.IsGx && !x.Active).FirstOrDefault();
            var gxOld = appOld.Repos.Where(x => x.OwnBuild.HasValue && x.OwnBuild.Value == nativeBT).Where(x => x.RepoName == repo.RepoName).FirstOrDefault();
            var envOlds = await _azureService.GetReleaseDefinitionEnvironmentsAsync(gxOld.ReleaseDefinitionId);
            EnvironmentHelper.UpdateProjectType(envOlds, AzureProjectType.GenexusOld.ToProjectTypeString());
            EnvironmentHelper.UpdateVisible(envOlds, false);

            foreach (var eo in envOlds)
            {
                environments.Add(eo);
            }
            return environments;
        }

        public async Task<ICollection<EnvironmentDto>> GetEnvironmentDtosScriptsAsync(string? repositoryId = null)
        {
            var apps = _configuration.GetSection("Projects").Get<List<ProjectSetting>>().Where(x => !x.IsGx && x.Active).FirstOrDefault();
            var aplicativos = apps.Repos;
            RepoSetting repo = null;
            if (repositoryId == null)
            {
                repo = aplicativos?.First()!;
            }
            else
            {
                repo = aplicativos?.First(x => x.RepoName == repositoryId)!;
            }
            _azureService.ForceProyectType(AzureProjectType.Default);

            var releaseDef = await _azureService.GetReleaseDefinitionsAsync();
            if (releaseDef == null)
            {
                return null;                
            }
            var defId = repo.ReleaseDefinitionId;

            var environments = await _azureService.GetReleaseDefinitionEnvironmentsAsync(defId);
            EnvironmentHelper.UpdateProjectType(environments, AzureProjectType.Default.ToProjectTypeString());

            /*olds*/
            _azureService.ForceProyectType(AzureProjectType.ScriptOld);
            var appOld = _configuration.GetSection("Projects").Get<List<ProjectSetting>>().Where(x => !x.IsGx && !x.Active).FirstOrDefault();
            var gxOld = appOld.Repos.Where(x => x.RepoName == repo.RepoName).FirstOrDefault();
            var envOlds = await _azureService.GetReleaseDefinitionEnvironmentsAsync(gxOld.ReleaseDefinitionId);
            EnvironmentHelper.UpdateProjectType(envOlds, AzureProjectType.ScriptOld.ToProjectTypeString());
            EnvironmentHelper.UpdateVisible(envOlds, false);

            foreach (var eo in envOlds)
            {
                environments.Add(eo);
            }
            return environments;
        }
    }
}
