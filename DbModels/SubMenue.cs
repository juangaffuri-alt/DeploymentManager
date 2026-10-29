using System;
using System.Collections.Generic;

namespace PasadorScript.DbModels;

public partial class SubMenue
{
    public int IdSubmenu { get; set; }

    public int IdMenu { get; set; }

    public string IdPermiso { get; set; } = null!;

    public string? AspController { get; set; }

    public string? AspAction { get; set; }

    public string? Descripcion { get; set; }

    public virtual Menue IdMenuNavigation { get; set; } = null!;
}
