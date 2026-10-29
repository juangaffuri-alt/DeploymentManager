using System;
using System.Collections.Generic;

namespace PasadorScript.DbModels;

public partial class TurnosModificacion
{
    public int Id { get; set; }

    public int EntornoId { get; set; }

    public string UsuarioId { get; set; } = null!;

    public DateTime FechaSolicitada { get; set; }

    public string NombreUsuario { get; set; } = null!;

    public DateTime FechaSolicitud { get; set; }

    public int MinutosEstimados { get; set; }

    public DateTime? InicioModificacion { get; set; }

    public DateTime? FinEstimado { get; set; }

    public int Estado { get; set; }

    public string? Motivo { get; set; }

    public int MinutosParaEjecucion { get; set; }

    public DateTime? InicioEjecucion { get; set; }

    public DateTime? FechaEjecucion { get; set; }

    public virtual Entorno Entorno { get; set; } = null!;
}
