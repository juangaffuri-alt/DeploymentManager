using System;
using System.Collections.Generic;

namespace PasadorScript.DbModels;

public partial class Script
{
    public int IdScript { get; set; }

    public string? Descripcion { get; set; }

    public string Usuario { get; set; } = null!;

    public DateTime FechaCreacion { get; set; }

    public int IdTipoCambio { get; set; }

    public int NroIncidente { get; set; }

    public string Aplicacion { get; set; } = null!;

    public string BranchNameDesa { get; set; } = null!;

    public int? PullRequestIdQa { get; set; }

    public string? CommitId { get; set; }

    public int? PullRequestIdprd { get; set; }

    public string? BranchNameQa { get; set; }

    public string? BranchNamePr { get; set; }

    public string? Files { get; set; }

    public string AzureProjectType { get; set; } = null!;

    public bool Deleted { get; set; }


    public bool IsHotfix { get; set; }

    public bool? SinPrueba { get; set; }

    public string? TareasJson { get; set; }

    public virtual TiposCambio IdTipoCambioNavigation { get; set; } = null!;

    public virtual ICollection<PullRequest> PullRequests { get; set; } = new List<PullRequest>();
}
