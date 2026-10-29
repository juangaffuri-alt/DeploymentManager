using System;
using System.Collections.Generic;

namespace PasadorScript.DbModels;

public partial class Entorno
{
    public int Id { get; set; }

    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    public string? Color { get; set; }

    public string? Icono { get; set; }

    public bool Activo { get; set; }

    public DateTime FechaCreacion { get; set; }

    public virtual ICollection<TurnosModificacion> TurnosModificacions { get; set; } = new List<TurnosModificacion>();
}
