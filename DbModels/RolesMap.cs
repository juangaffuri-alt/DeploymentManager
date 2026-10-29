using System;
using System.Collections.Generic;

namespace PasadorScript.DbModels;

public partial class RolesMap
{
    public int IdRol { get; set; }

    public string Descripcion { get; set; } = null!;
}
