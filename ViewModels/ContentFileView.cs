using PasadorScript.DbModels;
using PasadorScript.DTO.AzureDev;

namespace PasadorScript.ViewModels
{

    public class ContentFileView
    {
        public AzureDevopsPullRequest? PullRequest { get; set; }
        public string AzureProjectType { get; set; } = "Default";
        public string RepositoryId { get; set; }
        public int IdScript { get; set; }
        public string DescripcionTipoPasaje { get; set; }
        public string Usuario { get; set; }
        public int NumeroIncidente { get; set; }
        public string DescripcionScript { get; set; }
        public int? PullRequestIdQA { get; set; }
        public int? PullRequestIdPR { get; set; }
        public string BranchNameQA { get; set; }
        public string BranchNameDesa { get; set; }
        public string BranchNamePR { get; set; }
        public int Vote { get; set; } // 10, 5, -10
        public string Comment { get; set; }
        public string FilesJson { get; set; }
        public bool IsHotfix { get; set; }
        public string? TareasJson { get; set; }
        public ICollection<FileView> Files { get; set; } = new List<FileView>();
        public ICollection<Historico> Eventos { get; set; } = new List<Historico>();
        public ICollection<PullRequest> PullRequestHistoricos { get; set; } = new List<PullRequest>();
        public AzureDevopsPullRequestExtend ToExtendViewModel()
        {
            // Seguridad: si no hay PullRequest devolvemos uno con los datos mínimos
            if (this.PullRequest == null)
            {
                return new AzureDevopsPullRequestExtend
                {
                    IdType = this.IdScript,
                    Type = TipoCambio.Scripts,
                    DescripcionIncidente = this.DescripcionTipoPasaje,
                    Descripcion = this.DescripcionScript ?? string.Empty,
                    NroIncidente = this.NumeroIncidente
                };
            }

            // Si ya es la clase extendida, reutilizarla y asignar los campos adicionales
            if (this.PullRequest is AzureDevopsPullRequestExtend existingExtend)
            {
                existingExtend.IdType = this.IdScript;
                existingExtend.Type = TipoCambio.Scripts;
                existingExtend.DescripcionIncidente = this.DescripcionTipoPasaje;
                existingExtend.Descripcion = this.DescripcionScript ?? string.Empty;
                existingExtend.NroIncidente = this.NumeroIncidente;
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

            extend.IdType = this.IdScript;
            extend.Type = TipoCambio.Scripts;
            extend.DescripcionIncidente = this.DescripcionTipoPasaje;
            extend.Descripcion = this.DescripcionScript ?? string.Empty;
            extend.NroIncidente = this.NumeroIncidente;

            return extend;
        }

    }
}
