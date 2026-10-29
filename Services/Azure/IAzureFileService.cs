using PasadorScript.DTO.AzureDev;
using PasadorScript.ViewModels;

namespace PasadorScript.Services.Azure
{
    public interface IAzureFileService
    {
        Task<string> PushFiles(string repositoryId, string branchName, List<VirtualFileViewModel> files, bool firstCommit);
        Task<string> PushFiles(string repositoryId, string branchName, string nameFile, string contentFile);
        Task<string> GetFileContentAsync(string repositoryId, string filePath, bool? desdeBranchDesa, string? branchSource);
        Task<string> GetFileContentAsync(string repositoryId, string filePath, string commitId);
        Task<string> GetFileContentFromPathAsync(string repositoryId, string filePath);
        Task<string> GetActualPathAzureDevOps(string repositoryId, string path);
        Task<List<ArchivoConFecha>> GetArchivosEnCarpeta(string repositoryId, string branchName, string folderPath);
        Task<DateTime?> GetLastModifiedDateAsync(string repositoryId, string filePath, string branchName);
        Task<List<SqlStructure>> SearchSqlStructuresAsync(string repositoryId, string structureType = null, string searchTerm = null);
        Task<GitCommit?> GetLastModifiedAsync(string repositoryId, string filePath);
        Task<List<StructureInfo>> SearchStructures(string repositoryId, string structureType, string searchTerm);
    }
}
