using PasadorScript.DTO.AzureDev;
using PasadorScript.DTO.TeamDev;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace PasadorScript.ViewModels
{

    public class SearchCommits : GenericPaginator<BuildGXViewModel>
    {
        public string? Usuario { get; set; } = null;
        public string? SearchQuery { get; set; } = null;
        public string? CommitId { get; set; }
        [DisplayName("Knowledge base")]
        public string? RepositoryId { get; set; } = null;
        public string AzureProjectType { get; set; } = "Genexus";
        public string? Descripcion { get; set; }
        [DisplayName("Deploy activo y pendientes")]
        public bool SoloPendientes { get; set; } = false;
        public ICollection<EnvironmentDto> Environments { get; set; } = new List<EnvironmentDto>();
        [DisplayName("Anulados")]
        public bool Deleted { get; set; } = false;
        [DisplayName("Nro. incidente")]
        public int? NroIncidente { get; set; }
        [DisplayName("Objeto GX")]
        [MaxLength(100)]
        public string? ObjetoGx { get; set; }
        public void AppendEnvironments(ICollection<EnvironmentDto> environments)
        {
            foreach (var e in environments)
            {
                this.Environments.Add(e);
            }
        }
    }
}
