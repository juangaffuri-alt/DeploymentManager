using DocumentFormat.OpenXml.Wordprocessing;
using PasadorScript.DbModels;
using PasadorScript.DTO.Informes;
using PasadorScript.Helpers;
using PasadorScript.ViewModels;

namespace PasadorScript.Repository
{
    public interface IDbRepository
    {
        Task<bool> Descartar(int id, string tableName, bool deleted);
        Task<DbAction> UpdatePasaje(ContentFileView model);
        Task<ICollection<TiposCambio>> GetTiposCambios();
        Task<SearchScripts> GetScripts(bool? deleted, string? username = null, string? descripcion = null, string? repositoryId = null, int page = 1, int idEstado = 0, List<int>? prIds = null, int? nroIncidente = null);
        Task<ContentFileView> ScriptToViewModel(int id);
        Task<string> GetDescripcionPerfil(int id);
        Task<IEnumerable<Pat>> GetPats(string? username);
        Task SaveNewPat(string username, string pat);
        Task<BuildGXViewModel> BuildGxToViewModel(int id);
        Task<SearchCommits> GetBuildsGx(bool? deleted, string? repositoryId, string? username, string? descripcion, int page = 1, List<int>? prIds = null, int? nroIncidente = null, string? objetoGx = null);
        Task<BuildsGx> GetBuildById(int id);
        Task<BuildsGx> GetBuildByIdPullRequest(int id);
        Task<BuildsGx> AddBuild(BuildGXViewModelAndCommitsViewModel model);
        Task<BuildsGx> Edit(BuildGXViewModelAndCommitsViewModel model);
        Task<ScriptRequestViewModel> ScriptToRequestViewModel(int id);
        Task<Script?> GetScriptById(int id);
        Task<Script> AddPasaje(ScriptRequestViewModel model);
        Task<Script> UpdatePasaje(ScriptRequestViewModel model);
        Task<Script> GetScriptByPullRequest(int pullRequestId);
        /// <summary>
        /// Resolves the hotfix flag of the local envío associated with a pull request.
        /// </summary>
        /// <param name="pullRequestId">The Azure DevOps pull request identifier.</param>
        /// <returns>
        /// <see langword="true" /> if the envío is a hotfix; <see langword="false" /> if it is not;
        /// <see langword="null" /> if <paramref name="pullRequestId"/> is not positive or no matching envío exists.
        /// </returns>
        Task<bool?> GetIsHotfixByPullRequestAsync(int pullRequestId);

        /// <summary>
        /// Graba <c>sinPrueba</c> en el envío local del pull request.
        /// Busca primero <c>Scripts</c>, después <c>BuildsGX</c> y después <c>EnviosDlya</c>.
        /// </summary>
        /// <param name="pullRequestId">Identificador del pull request en Azure.</param>
        /// <param name="sinPrueba"><see langword="true"/> si la aprobación fue Sin Prueba.</param>
        /// <returns><see langword="true"/> si actualizó una fila; <see langword="false"/> si no hay envío local.</returns>
        Task<bool> GuardarSinPruebaPorPullRequestAsync(int pullRequestId, bool sinPrueba);
        Task<DbAction> UpdatePasaje(BuildGXViewModelAndCommitsViewModel model);
        Task<DbAction> UpdatePasaje(BuildsGx model);
        Task<bool> DeleteGx(int idBuild);
        Task<PasajeConPullRequest> IdToGenericModel(bool gx, int id);
        Task<bool> DeletePat(string username);
        Task<bool> DeleteScript(int id);
        Task<ICollection<string>> GetUsuarios(TablasType tablasType, string term);
        /// <summary>
        /// Obtiene envios locales para el informe gerencial de releases (sin paginar).
        /// Los nombres de objetos y archivos se resuelven en SQL; el contenido de los archivos no forma parte del resultado.
        /// </summary>
        /// <param name="desde">Limite inferior inclusive de creacion local.</param>
        /// <param name="hasta">Limite superior inclusive de creacion local.</param>
        /// <param name="repoKeys">Keys de repositorio a incluir; <see langword="null"/> o vacio significa todos.</param>
        /// <param name="incluirScript"><see langword="true"/> para incluir filas Script; en caso contrario, <see langword="false"/>.</param>
        /// <param name="incluirGx"><see langword="true"/> para incluir filas GX; en caso contrario, <see langword="false"/>.</param>
        /// <param name="incluirDlya"><see langword="true"/> para incluir filas DLYA; en caso contrario, <see langword="false"/>.</param>
        /// <param name="hotfix">Si tiene valor, filtra por <c>IsHotfix</c>; si no, incluye ambos.</param>
        /// <param name="usuario">Filtro opcional de usuario (mismo criterio que los listados).</param>
        /// <param name="nroIncidente">Si es mayor a cero, filtra por <c>NroIncidente</c> exacto; si no, incluye todos.</param>
        /// <param name="tipoReferencia">Si tiene valor, filtra por descripcion de <c>TiposCambio</c>; si no, incluye todos.</param>
        /// <returns>Los envios proyectados ordenados por fecha de creacion descendente.</returns>
        Task<IReadOnlyList<EnvioInformeReleaseDto>> GetEnviosInformeReleasesAsync(
            DateTime desde,
            DateTime hasta,
            IReadOnlyCollection<string>? repoKeys,
            bool incluirScript,
            bool incluirGx,
            bool incluirDlya,
            bool? hotfix,
            string? usuario,
            int? nroIncidente,
            string? tipoReferencia);
        #region ENVIOS DLYA
        Task<SearchEnviosDlya> GetEnviosDlyaAsync(bool? deleted, string? username, string? descripcion, string? repositoryId = null, int page = 1, int? nroIncidente = null);
        Task<EnviosDlya?> GetEnvioDlya(int id);
        Task<EnviosDlya?> GetEnvioDlyaByIdPullRequest(int id);
        Task<EnviosDlya> AddEnvioDlya(EnvioDlyaRequestViewModel model);
        Task<EnviosDlya?> Edit(EnvioDlyaRequestViewModel model);
        Task<EnvioDlyaRequestViewModel?> EnvioDlyaToRequestViewModel(int id);
        Task<EnviosDlya> UpdatePasaje(EnvioDlyaRequestViewModel model);
        Task<DbAction> UpdatePasaje(EnvioDlyaViewModelAndCommitsViewModel model);
        Task<bool> DeleteEnvioDlya(int idEnvio);

        #endregion ENVIOS DLYA
    }
}