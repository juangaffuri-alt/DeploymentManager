using PasadorScript.ViewModels;

namespace PasadorScript.Services.Informes;

/// <summary>
/// Construye el informe gerencial de solo lectura: envios locales cruzados con el ultimo stage de Azure.
/// </summary>
public interface IInformeGerencialReleasesService
{
    /// <summary>
    /// Carga filtros, cruza envios locales con releases de Azure en lote y devuelve una pagina de filas
        /// mas los totales KPI de los graficos (envios y agrupados por referencia; la visibilidad y el agrupado son flags de UI).
    /// </summary>
    /// <param name="filtros">Valores actuales de filtro (las fechas pueden ir vacias; se aplican defaults).</param>
    /// <param name="fechaHastaInformada">
    /// <see langword="true" /> si el usuario eligio fecha hasta en el selector (fin de dia inclusive);
    /// si no, el tope superior es ahora.
    /// </param>
    /// <param name="page">Numero de pagina solicitado (base 1).</param>
    /// <returns>El view model poblado, con advertencias si Azure falla o el listado se trunca.</returns>
    Task<InformeGerencialReleasesFilterViewModel> ObtenerAsync(
        InformeGerencialReleasesFilterViewModel filtros,
        bool fechaHastaInformada,
        int page);

    /// <summary>
    /// Carga el mismo universo filtrado que <see cref="ObtenerAsync"/> sin paginar, para exportar a Excel.
    /// </summary>
    /// <param name="filtros">Valores actuales de filtro (las fechas pueden ir vacias; se aplican defaults).</param>
    /// <param name="fechaHastaInformada">
    /// <see langword="true" /> si el usuario eligio fecha hasta en el selector (fin de dia inclusive);
    /// si no, el tope superior es ahora.
    /// </param>
    /// <returns>Las filas filtradas luego del cruce con Azure y el filtro de stage, en el mismo orden que el informe en pantalla.</returns>
    Task<IReadOnlyList<InformeGerencialReleaseRowViewModel>> ObtenerFilasAsync(
        InformeGerencialReleasesFilterViewModel filtros,
        bool fechaHastaInformada);
}
