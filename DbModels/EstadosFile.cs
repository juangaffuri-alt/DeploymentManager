using System;
using System.Collections.Generic;

namespace PasadorScript.DbModels;

public partial class EstadosFile
{
    public int IdEstado { get; set; }

    public string Descripcion { get; set; } = null!;
}
