using X.PagedList;

namespace PasadorScript.ViewModels;

/// <summary>
/// Opcion de aplicativo / pipeline para el filtro del informe gerencial de releases.
/// </summary>
public sealed class InformeGerencialAplicativoOption
{
    /// <summary>Valor de filtro (nombre visible). Repos coincidentes comparten esta etiqueta.</summary>
    public string RepoKey { get; init; } = string.Empty;

    /// <summary>Etiqueta mostrada en el combo.</summary>
    public string DisplayName { get; init; } = string.Empty;
}

/// <summary>
/// Opcion de bucket generico de ultimo stage para el filtro del informe gerencial de releases.
/// </summary>
public sealed class InformeGerencialStageOption
{
    /// <summary>Valor de filtro (<c>Key</c> de configuracion).</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>Etiqueta mostrada en el combo.</summary>
    public string Display { get; init; } = string.Empty;
}

/// <summary>
/// Modelo de filtros y resultado de <c>InformesGerenciales/Releases</c>.
/// </summary>
public class InformeGerencialReleasesFilterViewModel
{
    /// <summary>Fecha desde inclusive del filtro de creacion local.</summary>
    public DateTime? FechaDesde { get; set; }

    /// <summary>Fecha hasta del filtro de creacion local (dia calendario inclusive si se eligio en el selector).</summary>
    public DateTime? FechaHasta { get; set; }

    /// <summary>Dia calendario inclusive de inicio del filtro de implementacion en produccion. Vacio significa extremo abierto.</summary>
    public DateTime? FechaImplementadoDesde { get; set; }

    /// <summary>Dia calendario inclusive de fin del filtro de implementacion en produccion. Vacio significa extremo abierto.</summary>
    public DateTime? FechaImplementadoHasta { get; set; }

    /// <summary>Nombre visible del aplicativo seleccionado; vacio significa todos los repos configurados (actuales e historicos).</summary>
    public string? RepoKey { get; set; }

    /// <summary>Filtro de tipo de envio; vacio significa todos los tipos.</summary>
    public string? Tipo { get; set; }

    /// <summary>Filtro de hotfix; <see langword="null"/> significa todos.</summary>
    public bool? Hotfix { get; set; }

    /// <summary>Filtro de usuario.</summary>
    public string? Usuario { get; set; }

    /// <summary>Key del bucket de ultimo stage, o el centinela de sin release. Vacio significa todos.</summary>
    public string? UltimoStage { get; set; }

    /// <summary>Filtro de numero de referencia de negocio; vacio o no mayor a cero significa todos.</summary>
    public int? NroIncidente { get; set; }

    /// <summary>Filtro de tipo de referencia (<c>TiposCambio.Descripcion</c>); vacio significa todos.</summary>
    public string? TipoReferencia { get; set; }

    /// <summary>
    /// Indica si se debe mostrar el grafico de hotfix.
    /// El valor por defecto es <see langword="false"/>.
    /// </summary>
    public bool MostrarGraficoHotfix { get; set; }

    /// <summary>
    /// Indica si se debe mostrar el grafico de destino.
    /// El valor por defecto es <see langword="false"/>.
    /// </summary>
    public bool MostrarGraficoDestino { get; set; }

    /// <summary>
    /// Indica si se debe mostrar el grafico de prueba acotada.
    /// El valor por defecto es <see langword="false"/>.
    /// </summary>
    public bool MostrarGraficoPruebaAcotada { get; set; }

    /// <summary>
    /// Indica si el grafico de hotfix cuenta referencias distintas en lugar de envios.
    /// El valor por defecto es <see langword="false"/> (el usuario lo activa por grafico).
    /// </summary>
    public bool AgruparGraficoHotfix { get; set; }

    /// <summary>
    /// Indica si el grafico de destino cuenta referencias distintas en lugar de envios.
    /// El valor por defecto es <see langword="false"/> (el usuario lo activa por grafico).
    /// </summary>
    public bool AgruparGraficoDestino { get; set; }

    /// <summary>
    /// Indica si el grafico de prueba acotada cuenta referencias distintas en lugar de envios.
    /// El valor por defecto es <see langword="false"/> (el usuario lo activa por grafico).
    /// </summary>
    public bool AgruparGraficoPruebaAcotada { get; set; }

    /// <summary>Totales de mix hotfix por envio sobre el universo filtrado (antes de paginar).</summary>
    public InformeGerencialKpiHotfixViewModel KpiHotfix { get; set; } = new();

    /// <summary>Totales de mix hotfix agrupados por tipo y numero de referencia.</summary>
    public InformeGerencialKpiHotfixViewModel KpiHotfixReferencias { get; set; } = new();

    /// <summary>Totales de destino por envio sobre el universo filtrado (antes de paginar).</summary>
    public InformeGerencialKpiDestinoViewModel KpiDestino { get; set; } = new();

    /// <summary>Totales de destino agrupados por tipo y numero de referencia.</summary>
    public InformeGerencialKpiDestinoViewModel KpiDestinoReferencias { get; set; } = new();

    /// <summary>Totales de prueba acotada por envio sobre el universo filtrado (antes de paginar).</summary>
    public InformeGerencialKpiPruebaAcotadaViewModel KpiPruebaAcotada { get; set; } = new();

    /// <summary>Totales de prueba acotada agrupados por tipo y numero de referencia.</summary>
    public InformeGerencialKpiPruebaAcotadaViewModel KpiPruebaAcotadaReferencias { get; set; } = new();

    /// <summary>
    /// Cantidad de referencias de negocio distintas
    /// (<c>TipoCambioDescripcion</c> + <c>NroIncidente</c>) en el universo filtrado antes de paginar.
    /// Las filas sin numero (<c>NroIncidente</c> &lt;= 0) cuentan cada una por separado.
    /// </summary>
    public int TotalReferencias { get; set; }

    /// <summary>
    /// Indica si el rango de fechas coincide con el default (ultimo mes hasta hoy).
    /// </summary>
    public bool FechasSonDefault { get; set; }

    /// <summary>Filas paginadas luego del cruce con Azure y el filtro de stage.</summary>
    public IPagedList<InformeGerencialReleaseRowViewModel> Filas { get; set; }
        = new StaticPagedList<InformeGerencialReleaseRowViewModel>([], 1, 20, 0);

    /// <summary>Opciones del combo de aplicativo.</summary>
    public IReadOnlyList<InformeGerencialAplicativoOption> Aplicativos { get; set; } = [];

    /// <summary>Descripciones de tipo de referencia para el combo (catalogo <c>TiposCambio</c>).</summary>
    public IReadOnlyList<string> TiposReferencia { get; set; } = [];

    /// <summary>Buckets genericos de stage desde configuracion (sin Todos ni Sin release).</summary>
    public IReadOnlyList<InformeGerencialStageOption> Stages { get; set; } = [];

    /// <summary>Advertencia no bloqueante por fallos de Azure o listados truncados.</summary>
    public string? Advertencia { get; set; }

    /// <summary>
    /// Cantidad de chips aplicados excluyendo el rango de creacion
    /// (implementacion, app, tipo, hotfix, tipo de referencia, nro, usuario, stage, graficos).
    /// </summary>
    public int CantidadChipsSinFechas
    {
        get
        {
            var n = 0;
            if (FechaImplementadoDesde.HasValue) n++;
            if (FechaImplementadoHasta.HasValue) n++;
            if (!string.IsNullOrWhiteSpace(RepoKey)) n++;
            if (!string.IsNullOrWhiteSpace(Tipo)) n++;
            if (Hotfix.HasValue) n++;
            if (!string.IsNullOrWhiteSpace(TipoReferencia)) n++;
            if (NroIncidente is > 0) n++;
            if (!string.IsNullOrWhiteSpace(Usuario)) n++;
            if (!string.IsNullOrWhiteSpace(UltimoStage)) n++;
            if (MostrarGraficoHotfix) n++;
            if (MostrarGraficoDestino) n++;
            if (MostrarGraficoPruebaAcotada) n++;
            return n;
        }
    }

    /// <summary>
    /// Indica si se debe mostrar Limpiar (cualquier cosa distinta al rango de fechas default y sin filtros extra).
    /// </summary>
    public bool MostrarLimpiar => !FechasSonDefault || CantidadChipsSinFechas > 0;
}
