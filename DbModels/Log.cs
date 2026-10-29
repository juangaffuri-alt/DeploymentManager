using System;
using System.Collections.Generic;

namespace PasadorScript.DbModels;

public partial class Log
{
    public int Id { get; set; }

    public string? Message { get; set; }

    public string? Level { get; set; }

    public DateTime? TimeStamp { get; set; }

    public string? Exception { get; set; }

    public string? Service { get; set; }

    public string? UserId { get; set; }

    public string? RequestId { get; set; }
}
