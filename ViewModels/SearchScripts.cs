using PasadorScript.DbModels;
using PasadorScript.DTO.AzureDev;
using PasadorScript.DTO.Config;
using System.ComponentModel;

namespace PasadorScript.ViewModels
{
    public class SearchScripts : GenericPaginator<ScriptViewModel>
    {
        [DisplayName("Aplicativo")]
        public string? RepositoryId { get; set; } = null;
        public int? IdEstado { get; set; }
        public string? Descripcion { get; set; }
        public string? Usuario { get; set; }
        [DisplayName("Deploy activo y pendientes")]
        public bool SoloPendientes { get; set; } = false;
        public ICollection<EnvironmentDto> Environments { get; set; } = new List<EnvironmentDto>();
        [DisplayName("Anulados")]
        public bool Deleted { get; set; } = false;
        [DisplayName("Nro. incidente")]
        public int? NroIncidente { get; set; }

        /// <summary>
        /// Alias de stages Azure (InformeGerencial:Stages) para pintar iconos de envío cuando el definitionEnvironmentId no coincide.
        /// </summary>
        public IReadOnlyList<InformeGerencialStageBucket> StageBuckets { get; set; } = [];
    }
}
