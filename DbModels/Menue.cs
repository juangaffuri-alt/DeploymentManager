using System;
using System.Collections.Generic;

namespace PasadorScript.DbModels;

public partial class Menue
{
    public int IdMenu { get; set; }

    public int? IdPerfil { get; set; }

    public string Descripcion { get; set; } = null!;

    public string? AspController { get; set; }

    public string? AspAction { get; set; }

    public string? IClass { get; set; }

    public virtual ICollection<SubMenue> SubMenues { get; set; } = new List<SubMenue>();
}
