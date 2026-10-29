using System;
using System.Collections.Generic;

namespace PasadorScript.DbModels;

public partial class Pat
{
    public string Username { get; set; } = null!;

    public string? Pat1 { get; set; }

    public DateTime? FechaCreacion { get; set; }
}
