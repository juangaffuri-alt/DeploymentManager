using System;
using System.Collections.Generic;

namespace PasadorScript.DbModels;

public partial class HistorialScript
{
    public int IdHistorial { get; set; }

    public int IdScript { get; set; }

    public string Usuario { get; set; } = null!;

    public DateTime FechaHora { get; set; }

    public int IdEstado { get; set; }

    public string? Observacion { get; set; }

    public virtual Estado IdEstadoNavigation { get; set; } = null!;

    public virtual Script IdScriptNavigation { get; set; } = null!;
}
