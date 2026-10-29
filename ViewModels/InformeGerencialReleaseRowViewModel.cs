using PasadorScript.Helpers;

namespace PasadorScript.ViewModels;

/// <summary>
/// Fila de un envio local en el informe gerencial de releases.
/// </summary>
public sealed class InformeGerencialReleaseRowViewModel
{
    /// <summary>Tipo de envio (<c>Script</c>, <c>Gx</c> o <c>Dlya</c>).</summary>
    public string Tipo { get; init; } = string.Empty;

    /// <summary>Identificador local del envio.</summary>
    public int IdCambio { get; init; }

    /// <summary>
    /// Nombre visible del aplicativo del envio.
    /// Si el repositorio no esta mapeado, la clave del repositorio.
    /// </summary>
    public string Aplicativo { get; init; } = string.Empty;

    /// <summary>Descripcion del cambio.</summary>
    public string? Descripcion { get; init; }

    /// <summary>Indica si el envio es hotfix.</summary>
    public bool IsHotfix { get; init; }

    /// <summary>
    /// Marca de prueba acotada (<c>sinPrueba</c>).
    /// <see langword="true"/> es Sí, <see langword="false"/> es No y <see langword="null"/> es Sin datos.
    /// </summary>
    public bool? SinPrueba { get; init; }

    /// <summary>Texto visible de la marca de prueba acotada (Sí, No o Sin datos).</summary>
    public string PruebaAcotadaTexto => SinPrueba switch
    {
        true => "Sí",
        false => "No",
        _ => "Sin datos"
    };

    /// <summary>Fecha de creacion local del envio.</summary>
    public DateTime FechaCreacion { get; init; }

    /// <summary>Usuario autor.</summary>
    public string Usuario { get; init; } = string.Empty;

    /// <summary>Marca UTC de aprobacion PRE-deploy de QA (ingreso), si hubo.</summary>
    public DateTime? FechaIngresoQa { get; init; }

    /// <summary>Marca UTC de aprobacion POST-deploy de QA (egreso). Queda vacia si el POST fue rechazado o sigue pendiente.</summary>
    public DateTime? FechaEgresoQa { get; init; }

    /// <summary>
    /// Resultado del POST de QA del ultimo attempt (<c>Aprobado</c>, <c>Rechazado</c> o <c>Pendiente</c>).
    /// Vacio si no hubo environment QA, attempt, o el rechazo fue de entrada (PRE).
    /// </summary>
    public string? ResultadoQa { get; init; }

    /// <summary>Hora UTC de fin del deploy aplicado en produccion. Vacia si PROD no quedo aplicado.</summary>
    public DateTime? FechaImplementado { get; init; }

    /// <summary>Nombre del environment de mayor rank con deploy aplicado, si existe.</summary>
    public string? UltimoStageImplementado { get; init; }

    /// <summary>
    /// Nombre para mostrar de la persona no IYC que pre-aprobo produccion, si el release llego a PROD.
    /// </summary>
    public string? PreAprobo { get; init; }

    /// <summary>
    /// Nombre para mostrar de la persona del rol IyC que pre-aprobo produccion, si el release llego a PROD.
    /// </summary>
    public string? PreAproboIyc { get; init; }

    /// <summary>
    /// Nombres de objetos o archivos del envio, separados por coma y espacio.
    /// Vacio cuando el envio no tiene nombres.
    /// </summary>
    public string ObjetosArchivos { get; init; } = string.Empty;

    /// <summary>Identificador principal de pull request, si hay.</summary>
    public int? PullRequestId { get; init; }

    /// <summary>Tipo de proyecto Azure guardado con el envio (para links de estado de deploy).</summary>
    public string AzureProjectType { get; init; } = string.Empty;

    /// <summary>Numero de referencia de negocio (<c>NroIncidente</c>).</summary>
    public int NroIncidente { get; init; }

    /// <summary>Descripcion del tipo de cambio (por ejemplo INC, REQ).</summary>
    public string TipoCambioDescripcion { get; init; } = string.Empty;

    /// <summary>Texto de la referencia (tipo mas numero).</summary>
    public string Referencia => string.IsNullOrWhiteSpace(TipoCambioDescripcion)
        ? NroIncidente.ToString()
        : $"{TipoCambioDescripcion} {NroIncidente}";

    /// <summary>Fecha de creacion tal como esta persistida (ya en hora Argentina).</summary>
    public DateTime FechaCreacionLocal => FechaCreacion;

    /// <summary>Fecha de ingreso a QA en hora Argentina, o <see langword="null"/> si no hubo aprobacion PRE.</summary>
    public DateTime? FechaIngresoQaLocal => FechaIngresoQa.ToArgentinaTime();

    /// <summary>Fecha de egreso de QA en hora Argentina, o <see langword="null"/> si el POST no esta aprobado.</summary>
    public DateTime? FechaEgresoQaLocal => FechaEgresoQa.ToArgentinaTime();

    /// <summary>Fecha de aplicacion en produccion en hora Argentina, o <see langword="null"/> si no se aplico en PROD.</summary>
    public DateTime? FechaImplementadoLocal => FechaImplementado.ToArgentinaTime();
}
