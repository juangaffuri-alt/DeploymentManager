using System;
using System.Collections.Generic;

namespace PasadorScript.DbModels;

public partial class PullRequestsBuild
{
    public int IdBuild { get; set; }

    public int IdPr { get; set; }

    public virtual BuildsGx IdBuildNavigation { get; set; } = null!;
}
