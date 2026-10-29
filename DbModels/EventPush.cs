using System;
using System.Collections.Generic;

namespace PasadorScript.DbModels;

public partial class EventPush
{
    public int IdPush { get; set; }

    public string? Usuario { get; set; }

    public DateTime Fecha { get; set; }

    public string? Mensaje { get; set; }

    public int? TypeNotification { get; set; }

    public string? Additional { get; set; }
}
