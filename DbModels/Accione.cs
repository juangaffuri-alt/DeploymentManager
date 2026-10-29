using System;
using System.Collections.Generic;

namespace PasadorScript.DbModels;

public partial class Accione
{
    public int AccionId { get; set; }

    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    public string? Categoria { get; set; }

    public int? Orden { get; set; }

    public virtual ICollection<RolAccion> RolAccions { get; set; } = new List<RolAccion>();
}
