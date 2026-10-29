using PasadorScript.DTO.Auditoria;
using PasadorScript.DTO.AzureDev;
using PasadorScript.ViewModels;
using System.Text.Json;

namespace PasadorScript.Services.Azure
{
    public interface IAzureService: IAzureFileService, IAzureBranchService, IAzurePullRequestService,
                                IAzureBuildService, IAzureReleaseService, IAzureRepositoryService,
                                IAzureCommitService, IAzureAnalyticService, IAzurePatService
    {
        void ForceProyectType(AzureProjectType projectType);


    }
}
