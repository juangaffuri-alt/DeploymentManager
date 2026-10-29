using System;
using System.Collections.Generic;

namespace PasadorScript.DbModels;

public partial class Estado
{
    public int IdEstado { get; set; }

    public string Descripcion { get; set; } = null!;

    public string? BadgeClass { get; set; }

    public string? Icon { get; set; }

    public virtual ICollection<HistorialScript> HistorialScripts { get; set; } = new List<HistorialScript>();

    public virtual ICollection<Script> Scripts { get; set; } = new List<Script>();
}
