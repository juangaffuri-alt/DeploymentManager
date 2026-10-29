using PasadorScript.DTO.AzureDev;
using PasadorScript.Models.Reports;
using X.PagedList;

namespace PasadorScript.ViewModels
{
    public class ReleasesFilterViewModel
    {
        public List<ProjectSetting> Projects { get; set; }
        public string SelectedProjectName { get; set; }
        public int SelectedDefinitionId { get; set; }
        public bool SelectedIsGx { get; set; }
        public string SelectedStage { get; set; }          // Nuevo filtro
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
        public int PageSize { get; set; } = 20;
        public IPagedList<ReleaseViewModel> Releases { get; set; }
        public List<RepoSetting> CurrentRepos { get; set; }
        public List<string> Stages { get; set; }            // Lista de stages disponibles
    }
}
