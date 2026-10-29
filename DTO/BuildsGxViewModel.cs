using PasadorScript.DTO.AzureDev;

namespace PasadorScript.DTO
{
    public class BuildsGxViewModel
    {
        public int IdBuildGx { get; set; }

        public string? Descripcion { get; set; }

        public string Usuario { get; set; } = null!;

        public DateTime FechaCreacion { get; set; }

        public int IdTipoCambio { get; set; }

        public int NroIncidente { get; set; }

        public string? BranchName { get; set; }

        public string CommitListJson { get; set; } = null!;

        public int? IdPr { get; set; }
        public string DescripcionTipoCambio { get; set; } = null!;
        public ICollection<EnvironmentDto> Environments { get; set; } = new List<EnvironmentDto>();
    }
}
