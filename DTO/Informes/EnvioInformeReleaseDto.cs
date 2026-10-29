namespace PasadorScript.DTO.Informes;

/// <summary>
/// Proyeccion unificada de un envio local (Script, GX o DLYA) para el informe gerencial de releases.
/// </summary>
public class EnvioInformeReleaseDto
{
    /// <summary>Tipo de envio (<see cref="TipoEnvioInforme"/>).</summary>
    public string Tipo { get; set; } = string.Empty;

    /// <summary>Identificador local del envio.</summary>
    public int IdCambio { get; set; }

    /// <summary>Descripcion del cambio.</summary>
    public string? Descripcion { get; set; }

    /// <summary>Indica si el envio es hotfix.</summary>
    public bool IsHotfix { get; set; }

    /// <summary>
    /// Marca de prueba acotada (<c>sinPrueba</c>).
    /// Nula en envios anteriores al alta de la marca o que todavia no pasaron por la aprobacion que la graba.
    /// </summary>
    public bool? SinPrueba { get; set; }

    /// <summary>Fecha de creacion local del envio.</summary>
    public DateTime FechaCreacion { get; set; }

    /// <summary>Usuario autor.</summary>
    public string Usuario { get; set; } = string.Empty;

    /// <summary>Identificador principal de pull request, si hay.</summary>
    public int? PullRequestId { get; set; }

    /// <summary>Key de repositorio (<c>Aplicacion</c> o <c>RepositoryId</c>).</summary>
    public string RepoKey { get; set; } = string.Empty;

    /// <summary>Tipo de proyecto Azure guardado con el envio.</summary>
    public string AzureProjectType { get; set; } = string.Empty;

    /// <summary>Numero de referencia de negocio (<c>NroIncidente</c>).</summary>
    public int NroIncidente { get; set; }

    /// <summary>Descripcion del tipo de cambio (por ejemplo INC, REQ).</summary>
    public string TipoCambioDescripcion { get; set; } = string.Empty;

    /// <summary>
    /// Nombres de archivo u objeto separados por coma y espacio.
    /// Se arma en SQL y llega vacio cuando el envio no tiene nombres.
    /// </summary>
    public string ObjetosArchivos { get; set; } = string.Empty;
}
