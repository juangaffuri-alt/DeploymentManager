using System;
using System.Collections.Generic;

namespace PasadorScript.DbModels;

public partial class AdGroupMapping
{
    public string GroupGuid { get; set; } = null!;

    public string? GroupName { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? Email { get; set; }

    public virtual ICollection<RolAccion> RolAccions { get; set; } = new List<RolAccion>();
}
