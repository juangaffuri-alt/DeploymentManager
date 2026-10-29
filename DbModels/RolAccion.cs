using System;
using System.Collections.Generic;

namespace PasadorScript.DbModels;

public partial class RolAccion
{
    public int RolAccionId { get; set; }

    public string GroupGuid { get; set; } = null!;

    public int AccionId { get; set; }

    public DateTime? FechaAsignacion { get; set; }

    public string? AsignadoPor { get; set; }

    public virtual Accione Accion { get; set; } = null!;

    public virtual AdGroupMapping Group { get; set; } = null!;
}
