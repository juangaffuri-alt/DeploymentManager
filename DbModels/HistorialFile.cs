using System;
using System.Collections.Generic;

namespace PasadorScript.DbModels;

public partial class HistorialFile
{
    public int IdHistorial { get; set; }

    public DateTime FechaHora { get; set; }

    public string Usuario { get; set; } = null!;

    public bool EsProd { get; set; }

    public string? Result { get; set; }
}
