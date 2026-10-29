using PasadorScript.DbModels;

namespace PasadorScript.ViewModels
{
    public class ScriptViewModel
    {
        public int IdScript { get; set; }

        public string? Descripcion { get; set; }
        public string DescripcionShort
        {
            get
            {
                const int maxLength = 100;
                if (string.IsNullOrWhiteSpace(Descripcion))
                    return string.Empty;

                return Descripcion.Length <= maxLength
                    ? Descripcion
                    : Descripcion.Substring(0, maxLength) + "...";
            }
        }

        public string Usuario { get; set; } = null!;

        public DateTime FechaCreacion { get; set; }

        public int IdTipoCambio { get; set; }
        public string TipoCambioDescripcion { get; set; }

        public int NroIncidente { get; set; }
        public string AzureProjectType { get; set; } = "Default";

        public string Aplicacion { get; set; } = null!;

        public string BranchNameDesa { get; set; } = null!;

        public int? PullRequestIdQa { get; set; }

        public string? CommitId { get; set; }
        public bool Deleted { get; set; } = false;


        public bool IsHotfix { get; set; }

    }
}
