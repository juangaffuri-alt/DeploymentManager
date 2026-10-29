using PasadorScript.DTO.AzureDev;
using System.ComponentModel;

namespace PasadorScript.ViewModels
{
    public class SearchEnviosDlya : GenericPaginator<EnvioDlyaViewModel>
    {
        public string? Usuario { get; set; } = null;
        public string? SearchQuery { get; set; } = null;
        public string? CommitId { get; set; }
        [DisplayName("Knowledge base")]
        public string? RepositoryId { get; set; } = null;
        public string? Descripcion { get; set; }
        public ICollection<EnvironmentDto> Environments { get; set; } = new List<EnvironmentDto>();
        [DisplayName("Anulados")]
        public bool Deleted { get; set; } = false;
        [DisplayName("Nro. incidente")]
        public int? NroIncidente { get; set; }
    }
}
