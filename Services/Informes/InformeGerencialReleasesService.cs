using PasadorScript.DTO.AzureDev;
using PasadorScript.DTO.Config;
using PasadorScript.DTO.Informes;
using PasadorScript.Extensions;
using PasadorScript.Helpers;
using PasadorScript.Repository;
using PasadorScript.Services.Azure;
using PasadorScript.ViewModels;
using X.PagedList;
using X.PagedList.Extensions;

namespace PasadorScript.Services.Informes;

/// <summary>
/// Orquesta SQL y llamadas batch a Azure DevOps para armar el informe gerencial de releases.
/// </summary>
public sealed class InformeGerencialReleasesService : IInformeGerencialReleasesService
{
    /// <summary>Valor centinela del filtro de stage "Sin release".</summary>
    public const string FiltroSinRelease = "__sin_release__";

    private const int PageSize = 20;

    private readonly IDbRepository _dbRepository;
    private readonly IAzureReleaseService _azureReleaseService;
    private readonly IAzureBuildService _azureBuildService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InformeGerencialReleasesService> _logger;

    /// <summary>
    /// Inicializa una nueva instancia de la clase <see cref="InformeGerencialReleasesService"/>.
    /// </summary>
    public InformeGerencialReleasesService(
        IDbRepository dbRepository,
        IAzureReleaseService azureReleaseService,
        IAzureBuildService azureBuildService,
        IConfiguration configuration,
        ILogger<InformeGerencialReleasesService> logger)
    {
        _dbRepository = dbRepository ?? throw new ArgumentNullException(nameof(dbRepository));
        _azureReleaseService = azureReleaseService ?? throw new ArgumentNullException(nameof(azureReleaseService));
        _azureBuildService = azureBuildService ?? throw new ArgumentNullException(nameof(azureBuildService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<InformeGerencialReleasesFilterViewModel> ObtenerAsync(
        InformeGerencialReleasesFilterViewModel filtros,
        bool fechaHastaInformada,
        int page)
    {
        var (model, filas) = await ConstruirAsync(filtros, fechaHastaInformada);
        var lastPage = Math.Max(1, (int)Math.Ceiling(filas.Count / (double)PageSize));
        var pageNumber = Math.Clamp(page < 1 ? 1 : page, 1, lastPage);
        model.Filas = filas.ToPagedList(pageNumber, PageSize);
        return model;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<InformeGerencialReleaseRowViewModel>> ObtenerFilasAsync(
        InformeGerencialReleasesFilterViewModel filtros,
        bool fechaHastaInformada)
    {
        var (_, filas) = await ConstruirAsync(filtros, fechaHastaInformada);
        return filas;
    }

    private async Task<(InformeGerencialReleasesFilterViewModel Model, List<InformeGerencialReleaseRowViewModel> Filas)> ConstruirAsync(
        InformeGerencialReleasesFilterViewModel filtros,
        bool fechaHastaInformada)
    {
        ArgumentNullException.ThrowIfNull(filtros);

        var ahoraAr = DateTimeHelper.GetArgentinaNow();
        var fechaDesde = filtros.FechaDesde?.Date ?? ahoraAr.Date.AddMonths(-1);
        var fechaHastaPicker = filtros.FechaHasta?.Date ?? ahoraAr.Date;
        var desdeSql = DateTimeHelper.GetArgentinaStartOfDay(fechaDesde);
        var hastaSql = fechaHastaInformada
            ? DateTimeHelper.GetArgentinaEndOfDay(fechaHastaPicker)
            : ahoraAr;
        var minCreatedUtc = DateTimeHelper.ToUtcFromArgentina(desdeSql);

        filtros.FechaDesde = fechaDesde;
        filtros.FechaHasta = fechaHastaPicker;
        filtros.FechaImplementadoDesde = filtros.FechaImplementadoDesde?.Date;
        filtros.FechaImplementadoHasta = filtros.FechaImplementadoHasta?.Date;
        filtros.FechasSonDefault =
            fechaDesde == ahoraAr.Date.AddMonths(-1) && fechaHastaPicker == ahoraAr.Date;

        var repoMaps = LoadRepoMaps();
        filtros.Aplicativos = repoMaps
            .GroupBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var preferred = g
                    .OrderBy(r => EsHistorico(r.AzureProjectType) ? 1 : 0)
                    .First();
                return new InformeGerencialAplicativoOption
                {
                    RepoKey = preferred.DisplayName,
                    DisplayName = preferred.DisplayName
                };
            })
            .OrderBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var stageBuckets = LoadStageBuckets();
        var qaBucket = FindBucket(stageBuckets, "QA");
        var prodBucket = FindBucket(stageBuckets, "PROD");
        if (qaBucket == null)
            _logger.LogWarning("InformeGerencial:Stages no tiene bucket QA; las fechas de ingreso/egreso QA quedarán vacías");
        if (prodBucket == null)
            _logger.LogWarning("InformeGerencial:Stages no tiene bucket PROD; la porción En PROD del gráfico destino quedará en 0");

        filtros.Stages = stageBuckets
            .Select(b => new InformeGerencialStageOption
            {
                Key = b.Key,
                Display = string.IsNullOrWhiteSpace(b.Display) ? b.Key : b.Display
            })
            .ToList();

        await CargarTiposReferenciaAsync(filtros);

        var (incluirScript, incluirGx, incluirDlya) = ResolverTipos(filtros.Tipo);
        IReadOnlyCollection<string>? repoKeys = ResolverRepoKeys(filtros.RepoKey, repoMaps);

        var nroIncidente = filtros.NroIncidente is > 0 ? filtros.NroIncidente : null;
        filtros.NroIncidente = nroIncidente;
        var tipoReferencia = string.IsNullOrWhiteSpace(filtros.TipoReferencia) ? null : filtros.TipoReferencia.Trim();
        filtros.TipoReferencia = tipoReferencia;

        var envios = await _dbRepository.GetEnviosInformeReleasesAsync(
            desdeSql,
            hastaSql,
            repoKeys,
            incluirScript,
            incluirGx,
            incluirDlya,
            filtros.Hotfix,
            string.IsNullOrWhiteSpace(filtros.Usuario) ? null : filtros.Usuario.Trim(),
            nroIncidente,
            tipoReferencia);

        var (porPr, azureFailed, azureTruncated) = await CruzarAzureAsync(envios, repoMaps, minCreatedUtc);

        filtros.Advertencia = ArmarAdvertencia(azureFailed, azureTruncated);

        var byRepo = ToRepoLookup(repoMaps);
        var iycIds = LoadIycIdentityIds();
        var qaMissingLogged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var filas = envios
            .Select(envio => MapRow(envio, porPr, byRepo, iycIds, qaBucket, qaMissingLogged))
            .Where(row => PasaFiltroStage(row, filtros.UltimoStage, stageBuckets)
                          && PasaFiltroImplementacion(row, filtros.FechaImplementadoDesde, filtros.FechaImplementadoHasta))
            .ToList();

        filtros.KpiHotfix = CalcularKpiHotfix(filas);
        filtros.KpiHotfixReferencias = CalcularKpiHotfixAgrupado(filas);
        filtros.KpiDestino = CalcularKpiDestino(filas, prodBucket);
        filtros.KpiDestinoReferencias = CalcularKpiDestinoAgrupado(filas, prodBucket);
        filtros.KpiPruebaAcotada = CalcularKpiPruebaAcotada(filas);
        filtros.KpiPruebaAcotadaReferencias = CalcularKpiPruebaAcotadaAgrupado(filas);
        filtros.TotalReferencias = CalcularTotalReferencias(filas);

        return (filtros, filas);
    }

    private async Task<(
        IReadOnlyDictionary<PrJoinKey, ReleaseListItemDto> PorPr,
        bool Failed,
        bool Truncated)> CruzarAzureAsync(
        IReadOnlyList<EnvioInformeReleaseDto> envios,
        IReadOnlyList<RepoMap> repoMaps,
        DateTime minCreatedUtc)
    {
        var byRepo = ToRepoLookup(repoMaps);
        var unmapped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var definitions = new HashSet<DefinitionKey>();

        foreach (var envio in envios)
        {
            if (!TryResolveMap(envio, byRepo, out var map))
            {
                if (!string.IsNullOrWhiteSpace(envio.RepoKey))
                    unmapped.Add($"{envio.RepoKey} ({envio.AzureProjectType})");
                continue;
            }

            definitions.Add(new DefinitionKey(map.ProjectName, map.ReleaseDefinitionId));
        }

        foreach (var key in unmapped)
            _logger.LogWarning("RepoKey {RepoKey} no mapeado en configuración Projects", key);

        if (definitions.Count == 0)
            return (new Dictionary<PrJoinKey, ReleaseListItemDto>(), false, false);

        var listResults = await Task.WhenAll(definitions.Select(async def =>
        {
            var result = await _azureReleaseService.ListReleasesForReportAsync(
                def.ProjectName,
                def.ReleaseDefinitionId,
                minCreatedUtc);
            return result;
        }));

        var azureFailed = listResults.Any(r => r.Failed);
        var azureTruncated = listResults.Any(r => r.PageLimitReached);
        var allReleases = listResults.SelectMany(r => r.Releases).ToList();

        await CompletarPullRequestsDesdeBuildsAsync(allReleases);

        var porPr = new Dictionary<PrJoinKey, ReleaseListItemDto>();
        foreach (var release in allReleases)
        {
            if (!release.PullRequestId.HasValue || string.IsNullOrWhiteSpace(release.ProjectName))
                continue;

            var key = new PrJoinKey(Normalize(release.ProjectName), release.PullRequestId.Value);
            if (!porPr.TryGetValue(key, out var existing) || release.CreatedOn > existing.CreatedOn)
                porPr[key] = release;
        }

        var joined = new List<ReleaseListItemDto>();
        foreach (var envio in envios)
        {
            if (!TryResolveMap(envio, byRepo, out var map) || !envio.PullRequestId.HasValue)
                continue;

            if (porPr.TryGetValue(new PrJoinKey(Normalize(map.ProjectName), envio.PullRequestId.Value), out var release))
                joined.Add(release);
        }

        await _azureReleaseService.HydrateMissingApprovalsForReportAsync(joined);

        return (porPr, azureFailed, azureTruncated);
    }

    private async Task CompletarPullRequestsDesdeBuildsAsync(List<ReleaseListItemDto> releases)
    {
        var pendientes = releases
            .Where(r => !r.PullRequestId.HasValue && r.BuildId.HasValue && r.BuildId.Value > 0)
            .ToList();

        if (pendientes.Count == 0)
            return;

        var porProyecto = pendientes.GroupBy(r => r.ProjectName, StringComparer.OrdinalIgnoreCase);
        var buildToPr = new Dictionary<BuildJoinKey, int>();

        foreach (var grupo in porProyecto)
        {
            var ids = grupo.Select(r => r.BuildId!.Value).Distinct().ToList();
            var builds = await _azureBuildService.GetBuildsByIdsAsync(grupo.Key, ids);
            var projectKey = Normalize(grupo.Key);
            foreach (var build in builds)
            {
                var prId = PullRequestIdExtractor.TryFromBuild(build);
                if (prId.HasValue)
                    buildToPr[new BuildJoinKey(projectKey, build.Id)] = prId.Value;
            }
        }

        foreach (var release in pendientes)
        {
            if (!release.BuildId.HasValue || string.IsNullOrWhiteSpace(release.ProjectName))
                continue;

            var key = new BuildJoinKey(Normalize(release.ProjectName), release.BuildId.Value);
            if (buildToPr.TryGetValue(key, out var prId))
                release.PullRequestId = prId;
        }
    }

    private InformeGerencialReleaseRowViewModel MapRow(
        EnvioInformeReleaseDto envio,
        IReadOnlyDictionary<PrJoinKey, ReleaseListItemDto> porPr,
        IReadOnlyDictionary<RepoJoinKey, RepoMap> byRepo,
        IReadOnlySet<string> iycIds,
        InformeGerencialStageBucket? qaBucket,
        HashSet<string> qaMissingLogged)
    {
        DateTime? fechaImplementado = null;
        DateTime? fechaIngresoQa = null;
        DateTime? fechaEgresoQa = null;
        string? resultadoQa = null;
        string? ultimoStage = null;
        string? preAprobo = null;
        string? preAproboIyc = null;

        if (TryResolveMap(envio, byRepo, out var map)
            && envio.PullRequestId.HasValue
            && porPr.TryGetValue(new PrJoinKey(Normalize(map.ProjectName), envio.PullRequestId.Value), out var release))
        {
            ultimoStage = ResolverUltimoStageAplicado(release.Environments);
            fechaImplementado = ResolverFechaImplementadoProd(release.Environments);
            preAprobo = ResolverPreAprobo(release.Environments, iycIds, rolIyc: false);
            preAproboIyc = ResolverPreAprobo(release.Environments, iycIds, rolIyc: true);
            (fechaIngresoQa, fechaEgresoQa, resultadoQa) = ResolverFechasQa(
                release.Environments, qaBucket, envio.RepoKey, qaMissingLogged);
        }

        return new InformeGerencialReleaseRowViewModel
        {
            Tipo = envio.Tipo,
            IdCambio = envio.IdCambio,
            Aplicativo = ResolverAplicativo(envio, byRepo),
            Descripcion = envio.Descripcion,
            IsHotfix = envio.IsHotfix,
            SinPrueba = envio.SinPrueba,
            FechaCreacion = envio.FechaCreacion,
            Usuario = envio.Usuario,
            FechaIngresoQa = fechaIngresoQa,
            FechaEgresoQa = fechaEgresoQa,
            ResultadoQa = resultadoQa,
            FechaImplementado = fechaImplementado,
            UltimoStageImplementado = ultimoStage,
            PreAprobo = preAprobo,
            PreAproboIyc = preAproboIyc,
            ObjetosArchivos = envio.ObjetosArchivos,
            PullRequestId = envio.PullRequestId,
            AzureProjectType = envio.AzureProjectType,
            NroIncidente = envio.NroIncidente,
            TipoCambioDescripcion = envio.TipoCambioDescripcion
        };
    }

    private (DateTime? Ingreso, DateTime? Egreso, string? Resultado) ResolverFechasQa(
        IReadOnlyList<ReleaseEnvironmentListItemDto> environments,
        InformeGerencialStageBucket? qaBucket,
        string repoKey,
        HashSet<string> qaMissingLogged)
    {
        if (qaBucket == null)
            return (null, null, null);

        var qaEnv = environments
            .Where(e => NombreEnBucket(qaBucket, e.Name))
            .OrderByDescending(e => e.Rank)
            .FirstOrDefault();

        if (qaEnv == null)
        {
            if (environments.Count > 0 && qaMissingLogged.Add(repoKey ?? string.Empty))
            {
                _logger.LogWarning(
                    "No hay environment QA para RepoKey {RepoKey}. Environments {Environments}",
                    repoKey,
                    string.Join(", ", environments.Select(e => $"'{e.Name}'")));
            }

            return (null, null, null);
        }

        var maxAttempt = Math.Max(
            qaEnv.PreDeployApprovals.Select(a => a.Attempt).DefaultIfEmpty(0).Max(),
            qaEnv.PostDeployApprovals.Select(a => a.Attempt).DefaultIfEmpty(0).Max());

        if (maxAttempt <= 0)
            return (null, null, null);

        var ingreso = qaEnv.PreDeployApprovals
            .Where(a => a.Attempt == maxAttempt
                        && EsStatusAprobacion(a.Status, "approved")
                        && a.ModifiedOn.HasValue)
            .OrderByDescending(a => a.ModifiedOn)
            .FirstOrDefault()
            ?.ModifiedOn;

        var postsDelAttempt = qaEnv.PostDeployApprovals
            .Where(a => a.Attempt == maxAttempt)
            .ToList();

        var postAprobado = postsDelAttempt
            .Where(a => EsStatusAprobacion(a.Status, "approved") && a.ModifiedOn.HasValue)
            .OrderByDescending(a => a.ModifiedOn)
            .FirstOrDefault();

        var postRechazado = postsDelAttempt
            .Any(a => EsStatusAprobacion(a.Status, "rejected"));

        DateTime? egreso = postAprobado?.ModifiedOn;
        var resultado = ResolverResultadoQa(ingreso.HasValue, postAprobado != null, postRechazado);

        return (ingreso, egreso, resultado);
    }

    /// <summary>
    /// Resuelve el texto de Resultado QA segun el POST del ultimo attempt.
    /// Un rechazo PRE (sin POST) deja el resultado vacio: no hubo salida que calificar.
    /// </summary>
    /// <param name="hayIngreso">
    /// <see langword="true" /> si el PRE del ultimo attempt esta aprobado; en caso contrario, <see langword="false" />.
    /// </param>
    /// <param name="postAprobado">
    /// <see langword="true" /> si hay POST aprobado en el ultimo attempt; en caso contrario, <see langword="false" />.
    /// </param>
    /// <param name="postRechazado">
    /// <see langword="true" /> si hay POST rechazado en el ultimo attempt; en caso contrario, <see langword="false" />.
    /// </param>
    /// <returns>
    /// <c>Aprobado</c>, <c>Rechazado</c>, <c>Pendiente</c> o <see langword="null" /> si no hay salida que calificar.
    /// </returns>
    private static string? ResolverResultadoQa(bool hayIngreso, bool postAprobado, bool postRechazado)
    {
        if (postAprobado)
            return "Aprobado";

        if (postRechazado)
            return "Rechazado";

        if (hayIngreso)
            return "Pendiente";

        return null;
    }

    private static bool EsStatusAprobacion(string? status, string esperado)
        => string.Equals(status, esperado, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Carga el catalogo de tipos de referencia y conserva un valor filtrado que ya no este en el catalogo.
    /// </summary>
    /// <param name="filtros">El view model de filtros cuyo combo se completa.</param>
    private async Task CargarTiposReferenciaAsync(InformeGerencialReleasesFilterViewModel filtros)
    {
        var catalogo = (await _dbRepository.GetTiposCambios())
            .Select(t => t.Descripcion)
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => d.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!string.IsNullOrWhiteSpace(filtros.TipoReferencia)
            && !catalogo.Contains(filtros.TipoReferencia.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            catalogo.Add(filtros.TipoReferencia.Trim());
            catalogo.Sort(StringComparer.OrdinalIgnoreCase);
        }

        filtros.TiposReferencia = catalogo;
    }

    private static int CalcularTotalReferencias(
        IReadOnlyList<InformeGerencialReleaseRowViewModel> filas)
        => filas.Select(ClaveAgrupacionReferencia).Distinct(StringComparer.Ordinal).Count();

    /// <summary>
    /// Clave de agrupacion por tipo y numero de referencia.
    /// Los envios sin numero no se fusionan: cada uno cuenta por separado.
    /// </summary>
    private static string ClaveAgrupacionReferencia(InformeGerencialReleaseRowViewModel fila)
    {
        if (fila.NroIncidente <= 0)
            return $"envio:{fila.Tipo}:{fila.IdCambio}";

        var tipo = (fila.TipoCambioDescripcion ?? string.Empty).Trim().ToUpperInvariant();
        return $"ref:{tipo}:{fila.NroIncidente}";
    }

    /// <summary>
    /// Dia calendario inclusive en hora Argentina, la misma zona que <see cref="InformeGerencialReleaseRowViewModel.FechaImplementadoLocal"/>.
    /// Sin puntas no filtra. Con al menos una, un envio sin fecha de implementacion queda afuera.
    /// Si desde es posterior a hasta, no pasa ninguna fila.
    /// </summary>
    private static bool PasaFiltroImplementacion(
        InformeGerencialReleaseRowViewModel row,
        DateTime? desde,
        DateTime? hasta)
    {
        if (!desde.HasValue && !hasta.HasValue)
            return true;

        if (desde.HasValue && hasta.HasValue && desde.Value.Date > hasta.Value.Date)
            return false;

        var local = row.FechaImplementadoLocal;
        if (!local.HasValue)
            return false;

        var dia = local.Value.Date;
        if (desde.HasValue && dia < desde.Value.Date)
            return false;
        if (hasta.HasValue && dia > hasta.Value.Date)
            return false;

        return true;
    }

    private static InformeGerencialKpiPruebaAcotadaViewModel CalcularKpiPruebaAcotada(
        IReadOnlyList<InformeGerencialReleaseRowViewModel> filas)
    {
        var si = filas.Count(f => f.SinPrueba == true);
        var no = filas.Count(f => f.SinPrueba == false);
        return new InformeGerencialKpiPruebaAcotadaViewModel
        {
            Total = filas.Count,
            Si = si,
            No = no,
            SinDatos = filas.Count - si - no
        };
    }

    /// <summary>
    /// Calcula la marca de prueba acotada agrupando envios por tipo y numero de referencia.
    /// Una misma referencia puede contar en Sí, en No y en Sin datos.
    /// </summary>
    private static InformeGerencialKpiPruebaAcotadaViewModel CalcularKpiPruebaAcotadaAgrupado(
        IReadOnlyList<InformeGerencialReleaseRowViewModel> filas)
    {
        var grupos = filas.GroupBy(ClaveAgrupacionReferencia, StringComparer.Ordinal).ToList();
        return new InformeGerencialKpiPruebaAcotadaViewModel
        {
            Total = grupos.Count,
            Si = grupos.Count(g => g.Any(f => f.SinPrueba == true)),
            No = grupos.Count(g => g.Any(f => f.SinPrueba == false)),
            SinDatos = grupos.Count(g => g.Any(f => !f.SinPrueba.HasValue))
        };
    }

    private static InformeGerencialKpiHotfixViewModel CalcularKpiHotfix(
        IReadOnlyList<InformeGerencialReleaseRowViewModel> filas)
    {
        var hotfix = filas.Count(f => f.IsHotfix);
        return new InformeGerencialKpiHotfixViewModel
        {
            Total = filas.Count,
            Hotfix = hotfix,
            NoHotfix = filas.Count - hotfix
        };
    }

    /// <summary>
    /// Calcula el mix hotfix agrupando envios por tipo y numero de referencia.
    /// Una misma referencia puede contar en Hotfix y en No hotfix.
    /// </summary>
    /// <param name="filas">Las filas ya filtradas, antes de paginar.</param>
    /// <returns>Los totales por referencia distinta.</returns>
    private static InformeGerencialKpiHotfixViewModel CalcularKpiHotfixAgrupado(
        IReadOnlyList<InformeGerencialReleaseRowViewModel> filas)
    {
        var grupos = filas.GroupBy(ClaveAgrupacionReferencia, StringComparer.Ordinal).ToList();
        return new InformeGerencialKpiHotfixViewModel
        {
            Total = grupos.Count,
            Hotfix = grupos.Count(g => g.Any(f => f.IsHotfix)),
            NoHotfix = grupos.Count(g => g.Any(f => !f.IsHotfix))
        };
    }

    private static InformeGerencialKpiDestinoViewModel CalcularKpiDestino(
        IReadOnlyList<InformeGerencialReleaseRowViewModel> filas,
        InformeGerencialStageBucket? prodBucket)
    {
        var noSalieron = 0;
        var enProd = 0;
        var enQa = 0;

        foreach (var row in filas)
        {
            if (string.IsNullOrEmpty(row.UltimoStageImplementado))
            {
                noSalieron++;
                continue;
            }

            var esProd = prodBucket != null && NombreEnBucket(prodBucket, row.UltimoStageImplementado);

            if (esProd)
                enProd++;
            else
                enQa++;
        }

        return new InformeGerencialKpiDestinoViewModel
        {
            Total = filas.Count,
            NoSalieron = noSalieron,
            EnQa = enQa,
            EnProd = enProd
        };
    }

    /// <summary>
    /// Calcula el mix de destino agrupando envios por tipo y numero de referencia.
    /// Una misma referencia puede contar en mas de una porcion (por ejemplo QA y PROD).
    /// </summary>
    /// <param name="filas">Las filas ya filtradas, antes de paginar.</param>
    /// <param name="prodBucket">El bucket de produccion; si es <see langword="null"/>, ningun envio cuenta como PROD.</param>
    /// <returns>Los totales por referencia distinta.</returns>
    private static InformeGerencialKpiDestinoViewModel CalcularKpiDestinoAgrupado(
        IReadOnlyList<InformeGerencialReleaseRowViewModel> filas,
        InformeGerencialStageBucket? prodBucket)
    {
        var grupos = filas.GroupBy(ClaveAgrupacionReferencia, StringComparer.Ordinal).ToList();
        var noSalieron = 0;
        var enQa = 0;
        var enProd = 0;

        foreach (var grupo in grupos)
        {
            var tieneSinStage = false;
            var tieneQa = false;
            var tieneProd = false;

            foreach (var row in grupo)
            {
                if (string.IsNullOrEmpty(row.UltimoStageImplementado))
                {
                    tieneSinStage = true;
                    continue;
                }

                var esProd = prodBucket != null && NombreEnBucket(prodBucket, row.UltimoStageImplementado);
                if (esProd)
                    tieneProd = true;
                else
                    tieneQa = true;
            }

            if (tieneSinStage) noSalieron++;
            if (tieneQa) enQa++;
            if (tieneProd) enProd++;
        }

        return new InformeGerencialKpiDestinoViewModel
        {
            Total = grupos.Count,
            NoSalieron = noSalieron,
            EnQa = enQa,
            EnProd = enProd
        };
    }

    private static InformeGerencialStageBucket? FindBucket(
        IReadOnlyList<InformeGerencialStageBucket> buckets,
        string key)
        => buckets.FirstOrDefault(b => string.Equals(b.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Devuelve el nombre del environment de mayor <c>Rank</c> cuyo status es aplicado
    /// (<c>succeeded</c> o <c>partiallySucceeded</c>). Un rechazo o un stage en curso no cuenta.
    /// </summary>
    /// <param name="environments">Los environments del release.</param>
    /// <returns>El nombre del ultimo stage aplicado, o <see langword="null" /> si ninguno lo esta.</returns>
    private static string? ResolverUltimoStageAplicado(
        IReadOnlyList<ReleaseEnvironmentListItemDto> environments)
    {
        return environments
            .Where(e => EnvironmentDto.EsEstadoAplicado(e.Status))
            .OrderByDescending(e => e.Rank)
            .FirstOrDefault()
            ?.Name;
    }

    private static DateTime? ResolverFechaImplementadoProd(
        IReadOnlyList<ReleaseEnvironmentListItemDto> environments)
    {
        return environments
            .Where(e => EnvironmentDto.EsNombreProduccion(e.Name)
                        && EnvironmentDto.EsEstadoAplicado(e.Status)
                        && !EsEstadoRechazadoFallidoOCancelado(e.Status)
                        && e.CompletedOn.HasValue)
            .OrderByDescending(e => e.Rank)
            .FirstOrDefault()
            ?.CompletedOn;
    }

    private static bool EsEstadoRechazadoFallidoOCancelado(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return false;

        return status.Equals("rejected", StringComparison.OrdinalIgnoreCase)
               || status.Equals("failed", StringComparison.OrdinalIgnoreCase)
               || status.Equals("canceled", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Devuelve quien pre-aprobo produccion en el rol pedido.
    /// </summary>
    /// <param name="environments">Los environments del release.</param>
    /// <param name="iycIds">Los identificadores de identidad del grupo IyC.</param>
    /// <param name="rolIyc"><see langword="true" /> para la persona del rol IyC; <see langword="false" /> para la persona que no es de ese grupo.</param>
    /// <returns>El nombre de quien aprobo, o <see langword="null" /> si no hubo pre-aprobacion de ese rol.</returns>
    private static string? ResolverPreAprobo(
        IReadOnlyList<ReleaseEnvironmentListItemDto> environments,
        IReadOnlySet<string> iycIds,
        bool rolIyc)
    {
        var prod = environments
            .Where(e => e.QueuedOn.HasValue && EnvironmentDto.EsNombreProduccion(e.Name))
            .OrderByDescending(e => e.Rank)
            .FirstOrDefault();

        if (prod == null)
            return null;

        var candidato = prod.PreDeployApprovals
            .Where(a => !a.IsAutomated
                        && string.Equals(a.Status, "approved", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(a.ApprovedByDisplayName)
                        && EsGrupoIyc(a.ApproverId, a.ApproverDisplayName, iycIds) == rolIyc)
            .OrderByDescending(a => a.Attempt)
            .ThenByDescending(a => a.Rank)
            .ThenByDescending(a => a.ModifiedOn)
            .FirstOrDefault();

        return candidato?.ApprovedByDisplayName;
    }

    /// <summary>
    /// Devuelve el nombre visible del aplicativo, o la clave del repositorio si no hay mapa.
    /// </summary>
    /// <param name="envio">El envio local.</param>
    /// <param name="byRepo">Los mapas de repositorio por clave y tipo de proyecto.</param>
    /// <returns>El nombre visible, la clave del repositorio, o vacio si el envio no tiene clave.</returns>
    private static string ResolverAplicativo(
        EnvioInformeReleaseDto envio,
        IReadOnlyDictionary<RepoJoinKey, RepoMap> byRepo)
    {
        if (TryResolveMap(envio, byRepo, out var map))
            return map.DisplayName;

        return envio.RepoKey ?? string.Empty;
    }

    private IReadOnlySet<string> LoadIycIdentityIds()
    {
        var reviewers = _configuration.GetSection("AzureSettings:Reviewers").Get<List<ReviewerConfig>>() ?? [];
        return reviewers
            .Where(r => !string.IsNullOrWhiteSpace(r.Descripcion)
                        && r.Descripcion.Equals("IyC", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(r.Id))
            .Select(r => r.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool EsGrupoIyc(string? approverId, string? approverDisplayName, IReadOnlySet<string> iycIds)
    {
        if (!string.IsNullOrWhiteSpace(approverId) && iycIds.Contains(approverId))
            return true;

        return !string.IsNullOrWhiteSpace(approverDisplayName)
            && approverDisplayName.Contains("IYC", StringComparison.OrdinalIgnoreCase);
    }

    private static bool PasaFiltroStage(
        InformeGerencialReleaseRowViewModel row,
        string? ultimoStage,
        IReadOnlyList<InformeGerencialStageBucket> buckets)
    {
        if (string.IsNullOrWhiteSpace(ultimoStage))
            return true;

        if (string.Equals(ultimoStage, FiltroSinRelease, StringComparison.Ordinal))
            return string.IsNullOrEmpty(row.UltimoStageImplementado);

        var bucket = buckets.FirstOrDefault(b =>
            string.Equals(b.Key, ultimoStage, StringComparison.OrdinalIgnoreCase));
        if (bucket == null)
            return false;

        if (string.IsNullOrEmpty(row.UltimoStageImplementado))
            return false;

        return NombreEnBucket(bucket, row.UltimoStageImplementado);
    }

    /// <summary>
    /// Indica si <paramref name="name"/> pertenece a <paramref name="bucket"/> tras recortar espacios.
    /// </summary>
    /// <param name="bucket">Bucket de stage cuyos nombres configurados se comparan.</param>
    /// <param name="name">Nombre de environment de Azure, que puede traer espacios al inicio o al final.</param>
    /// <returns><see langword="true"/> si el nombre recortado coincide con una entrada del bucket; en caso contrario, <see langword="false"/>.</returns>
    private static bool NombreEnBucket(InformeGerencialStageBucket bucket, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var trimmed = name.Trim();
        return bucket.Nombres.Any(n =>
            string.Equals(n, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    private static (bool Script, bool Gx, bool Dlya) ResolverTipos(string? tipo)
    {
        if (string.IsNullOrWhiteSpace(tipo))
            return (true, true, true);

        return tipo switch
        {
            TipoEnvioInforme.Script => (true, false, false),
            TipoEnvioInforme.Gx => (false, true, false),
            TipoEnvioInforme.Dlya => (false, false, true),
            _ => (true, true, true)
        };
    }

    private static string? ArmarAdvertencia(bool azureFailed, bool azureTruncated)
    {
        if (azureFailed && azureTruncated)
            return "No se pudo completar la consulta a Azure DevOps y se alcanzó el tope de páginas. Los datos de release pueden estar incompletos.";

        if (azureFailed)
            return "No se pudo consultar Azure DevOps. Se muestran los envíos locales sin datos de release.";

        if (azureTruncated)
            return "Se alcanzó el tope de páginas de Azure DevOps. Algunos releases recientes podrían no aparecer.";

        return null;
    }

    private IReadOnlyList<InformeGerencialStageBucket> LoadStageBuckets()
    {
        var buckets = _configuration.GetSection("InformeGerencial:Stages")
            .Get<List<InformeGerencialStageBucket>>() ?? [];

        var valid = buckets
            .Where(b => !string.IsNullOrWhiteSpace(b.Key))
            .Select(b =>
            {
                b.Key = b.Key.Trim();
                b.Display = string.IsNullOrWhiteSpace(b.Display) ? b.Key : b.Display.Trim();
                b.Nombres = b.Nombres
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Select(n => n.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                return b;
            })
            .ToList();

        if (valid.Count == 0)
            _logger.LogWarning("InformeGerencial:Stages no está configurado o no tiene buckets válidos");

        return valid;
    }

    private static IReadOnlyCollection<string>? ResolverRepoKeys(
        string? seleccionado,
        IReadOnlyList<RepoMap> repoMaps)
    {
        if (string.IsNullOrWhiteSpace(seleccionado))
            return null;

        var porNombre = repoMaps
            .Where(r => string.Equals(r.DisplayName, seleccionado, StringComparison.OrdinalIgnoreCase))
            .Select(r => r.RepoKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (porNombre.Count > 0)
            return porNombre;

        var porRepoKey = repoMaps
            .Where(r => string.Equals(r.RepoKey, seleccionado, StringComparison.OrdinalIgnoreCase))
            .Select(r => r.RepoKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return porRepoKey.Count > 0 ? porRepoKey : [seleccionado];
    }

    private IReadOnlyList<RepoMap> LoadRepoMaps()
    {
        var projects = _configuration.GetSection("Projects").Get<List<ProjectSetting>>() ?? [];
        var maps = new List<RepoMap>();

        foreach (var project in projects)
        {
            if (string.IsNullOrWhiteSpace(project.ProjectName) || project.Repos == null)
                continue;

            var projectType = ResolveAzureProjectType(project);

            foreach (var repo in project.Repos)
            {
                if (string.IsNullOrWhiteSpace(repo.RepoName) || repo.ReleaseDefinitionId <= 0)
                    continue;

                var display = !string.IsNullOrWhiteSpace(repo.DisplayName)
                    ? repo.DisplayName
                    : !string.IsNullOrWhiteSpace(repo.PipelineName)
                        ? repo.PipelineName
                        : repo.RepoName;

                maps.Add(new RepoMap(
                    repo.RepoName,
                    projectType,
                    project.ProjectName,
                    repo.ReleaseDefinitionId,
                    display));
            }
        }

        return maps;
    }

    private Dictionary<RepoJoinKey, RepoMap> ToRepoLookup(IReadOnlyList<RepoMap> repoMaps)
    {
        var byRepo = new Dictionary<RepoJoinKey, RepoMap>();
        foreach (var map in repoMaps)
        {
            var key = new RepoJoinKey(Normalize(map.RepoKey), map.AzureProjectType);
            if (!byRepo.TryAdd(key, map))
            {
                _logger.LogWarning(
                    "RepoKey {RepoKey} con AzureProjectType {AzureProjectType} duplicado en configuración Projects; se usa ProjectName {ProjectName}",
                    map.RepoKey,
                    map.AzureProjectType.ToProjectTypeString(),
                    map.ProjectName);
            }
        }

        return byRepo;
    }

    private static bool TryResolveMap(
        EnvioInformeReleaseDto envio,
        IReadOnlyDictionary<RepoJoinKey, RepoMap> byRepo,
        out RepoMap map)
    {
        map = null!;
        if (string.IsNullOrWhiteSpace(envio.RepoKey)
            || !TryParseAzureProjectType(envio.AzureProjectType, out var projectType))
        {
            return false;
        }

        if (!byRepo.TryGetValue(new RepoJoinKey(Normalize(envio.RepoKey), projectType), out var found))
            return false;

        map = found;
        return true;
    }

    private static AzureProjectType ResolveAzureProjectType(ProjectSetting project)
    {
        if (project.IsGx)
            return project.Active ? AzureProjectType.GeneXus : AzureProjectType.GenexusOld;

        return project.Active ? AzureProjectType.Default : AzureProjectType.ScriptOld;
    }

    private static bool TryParseAzureProjectType(string? value, out AzureProjectType projectType)
    {
        projectType = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        switch (value.Trim().ToLowerInvariant())
        {
            case "default":
                projectType = AzureProjectType.Default;
                return true;
            case "genexus":
                projectType = AzureProjectType.GeneXus;
                return true;
            case "scriptold":
                projectType = AzureProjectType.ScriptOld;
                return true;
            case "genexusold":
                projectType = AzureProjectType.GenexusOld;
                return true;
            default:
                return false;
        }
    }

    private static bool EsHistorico(AzureProjectType projectType)
        => projectType is AzureProjectType.ScriptOld or AzureProjectType.GenexusOld;

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();

    private sealed record RepoMap(
        string RepoKey,
        AzureProjectType AzureProjectType,
        string ProjectName,
        int ReleaseDefinitionId,
        string DisplayName);

    private sealed record RepoJoinKey(string RepoKey, AzureProjectType AzureProjectType);

    private sealed record DefinitionKey(string ProjectName, int ReleaseDefinitionId);

    private sealed record PrJoinKey(string ProjectName, int PullRequestId);

    private sealed record BuildJoinKey(string ProjectName, int BuildId);
}
