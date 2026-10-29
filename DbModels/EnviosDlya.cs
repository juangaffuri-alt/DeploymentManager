using System;
using System.Collections.Generic;

namespace PasadorScript.DbModels;

public partial class EnviosDlya
{
    public int IdEnvioDlya { get; set; }

    public string Descripcion { get; set; } = null!;

    public string Usuario { get; set; } = null!;

    public DateTime FechaCreacion { get; set; }

    public int IdTipoCambio { get; set; }

    public int NroIncidente { get; set; }

    public string? BranchName { get; set; }

    public string CommitListJson { get; set; } = null!;

    public int? IdPr { get; set; }

    public string? RepositoryId { get; set; }

    public string AzureProjectType { get; set; } = null!;

    public bool Deleted { get; set; }

    public bool IsHotfix { get; set; }

    public bool? SinPrueba { get; set; }

    public string? TareasJson { get; set; }

    public virtual TiposCambio IdTipoCambioNavigation { get; set; } = null!;

    public virtual ICollection<PullRequestsEnviosDlya> PullRequestsEnviosDlyas { get; set; } = new List<PullRequestsEnviosDlya>();
}
