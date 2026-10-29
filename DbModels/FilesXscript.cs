using System;
using System.Collections.Generic;

namespace PasadorScript.DbModels;

public partial class FilesXscript
{
    public string IdFile { get; set; } = null!;

    public int IdScript { get; set; }

    public string FileName { get; set; } = null!;

    public DateTime FechaCreacion { get; set; }

    public int IdEstado { get; set; }

    public virtual EstadosFile IdEstadoNavigation { get; set; } = null!;

    public virtual Script IdScriptNavigation { get; set; } = null!;
}
