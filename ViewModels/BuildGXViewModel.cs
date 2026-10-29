using PasadorScript.DbModels;
using PasadorScript.DTO.AzureDev;
using PasadorScript.DTO.TeamDev;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace PasadorScript.ViewModels
{
    public class BuildGxViewWithClasses
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
        public string LabelIdentificador { get; set; }


        public BuildGxViewWithClasses(BuildsGx build)
        {
            this.IdBuildGx = build.IdBuildGx;
            this.Usuario = build.Usuario;
            this.Descripcion = build.Descripcion;
            this.FechaCreacion = build.FechaCreacion;
            this.IdTipoCambio = build.IdTipoCambio;
            this.NroIncidente = build.NroIncidente;
            this.BranchName = build.BranchName;
            this.CommitListJson = build.CommitListJson;
            this.IdPr = build.IdPr;
            this.LabelIdentificador = $"{build.IdTipoCambioNavigation.Descripcion} {build.NroIncidente}";
        }
    }
    public class BuildGXViewModelAndCommitsViewModel
    {
        public BuildGXViewModel modelCambio { get; set; }
        public List<CommitsTeamDev> commits { get; set; }
        public int? Vote { get; set; }
        public string? Comment { get; set; }
        public AzureDevopsPullRequest? PullRequest { get; set; }
        public AzureDevopsPullRequestExtend ToExtendViewModel()
        {
            // Seguridad: si no hay PullRequest devolvemos uno con los datos mínimos
            if (this.PullRequest == null)
            {
                return new AzureDevopsPullRequestExtend
                {
                    IdType = this.modelCambio.IdBuildGx,
                    Type = TipoCambio.Genexus,
                    DescripcionIncidente = this.modelCambio.DescripcionTipoCambio,
                    Descripcion = this.modelCambio.Descripcion ?? string.Empty,
                    NroIncidente = this.modelCambio.NroIncidente
                };
            }

            // Si ya es la clase extendida, reutilizarla y asignar los campos adicionales
            if (this.PullRequest is AzureDevopsPullRequestExtend existingExtend)
            {
                existingExtend.IdType = this.modelCambio.IdBuildGx;
                existingExtend.Type = TipoCambio.Genexus;
                existingExtend.DescripcionIncidente = this.modelCambio.DescripcionTipoCambio;
                existingExtend.Descripcion = this.modelCambio.Descripcion ?? string.Empty;
                existingExtend.NroIncidente = this.modelCambio.NroIncidente;
                return existingExtend;
            }

            // Si es la clase base, crear una nueva instancia extendida copiando propiedades relevantes
            var pr = this.PullRequest;
            var extend = new AzureDevopsPullRequestExtend
            {
                Repository = pr.Repository,
                PullRequestId = pr.PullRequestId,
                CodeReviewId = pr.CodeReviewId,
                Status = pr.Status,
                CreatedBy = pr.CreatedBy,
                CreationDate = pr.CreationDate,
                Title = pr.Title,
                Description = pr.Description,
                SourceRefName = pr.SourceRefName,
                TargetRefName = pr.TargetRefName,
                MergeStatus = pr.MergeStatus,
                MergeId = pr.MergeId,
                LastMergeSourceCommit = pr.LastMergeSourceCommit,
                LastMergeTargetCommit = pr.LastMergeTargetCommit,
                LastMergeCommit = pr.LastMergeCommit,
                Reviewers = pr.Reviewers,
                Url = pr.Url,
                Links = pr.Links,
                SupportsIterations = pr.SupportsIterations,
                ArtifactId = pr.ArtifactId
            };

            extend.IdType = this.modelCambio.IdBuildGx;
            extend.Type = TipoCambio.Genexus;
            extend.DescripcionIncidente = this.modelCambio.DescripcionTipoCambio;
            extend.Descripcion = this.modelCambio.Descripcion ?? string.Empty;
            extend.NroIncidente = this.modelCambio.NroIncidente;

            return extend;
        }
    }
    public class BuildGXViewModel
    {
        public int IdBuildGx { get; set; }
        [Display(Name = "Descripción")]
        [Required(ErrorMessage = "Campo obligatorio")]
        [MaxLength(255, ErrorMessage = "Ingreso máximo de 255 caracteres")]
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
        public string? Usuario { get; set; } = null!;

        public DateTime FechaCreacion { get; set; }
        [Required(AllowEmptyStrings = false, ErrorMessage = "Seleccione un valor de la lista")]
        [DisplayName("Clasificación")]

        public int IdTipoCambio { get; set; }
        public string? DescripcionTipoCambio { get; set; }
        [Required]
        [DisplayName("Número")]
        [Range(1, int.MaxValue, ErrorMessage = "El valor debe ser mayor o igual a 1")]
        public int NroIncidente { get; set; }

        public string? BranchName { get; set; }

        public string? CommitListJson { get; set; } = null!;
        public ICollection<CommitsTeamDev>? Commits { get; set; }
        public int? IdPr { get; set; }
        public string AzureProjectType { get; set; } = "Genexus";
        [DisplayName("Knowledge base")]
        public string? BtRepo { get; set; } = null!;
        public bool Deleted { get; set; } = false;
        public bool IsHotfix { get; set; }
        public string? TareasJson { get; set; }
    }
}
