using System;
using System.Collections.Generic;

namespace PasadorScript.DbModels;

public partial class TiposCambio
{
    public int IdTipoCambio { get; set; }

    public string Descripcion { get; set; } = null!;

    public virtual ICollection<BuildsGx> BuildsGxes { get; set; } = new List<BuildsGx>();

    public virtual ICollection<EnviosDlya> EnviosDlyas { get; set; } = new List<EnviosDlya>();

    public virtual ICollection<Script> Scripts { get; set; } = new List<Script>();
}
