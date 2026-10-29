using System;
using System.Collections.Generic;

namespace PasadorScript.DbModels;

public partial class PullRequestsEnviosDlya
{
    public int IdEnvioDlya { get; set; }

    public int IdPr { get; set; }

    public virtual EnviosDlya IdEnvioDlyaNavigation { get; set; } = null!;
}
