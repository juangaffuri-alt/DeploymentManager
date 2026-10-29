using System;
using System.Collections.Generic;

namespace PasadorScript.DbModels;

public partial class PullRequest
{
    public int IdScript { get; set; }

    public int IdPr { get; set; }

    public bool QaStage { get; set; }

    public virtual Script IdScriptNavigation { get; set; } = null!;
}
