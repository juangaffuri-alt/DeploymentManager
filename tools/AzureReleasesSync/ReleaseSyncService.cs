using AzureReleasesSync.Models;

namespace AzureReleasesSync;

/// <summary>
/// Orquesta la sincronización: determina la ventana (backfill o incremental),
/// consulta Azure por cada definición y vuelca los releases a la BD.
/// </summary>
public sealed class ReleaseSyncService
{
    private readonly SyncOptions _options;
    private readonly AzureReleaseClient _client;
    private readonly ReleaseRepository _repository;
    private readonly ILogger<Program> _logger;

    public ReleaseSyncService(
        SyncOptions options,
        AzureReleaseClient client,
        ReleaseRepository repository,
        ILogger<Program> logger)
    {
        _options = options;
        _client = client;
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Corre una sincronización. Con <paramref name="forceFullBackfill"/> ignora el watermark
    /// y pide <see cref="SyncOptions.BackfillDays"/> días hacia atrás.
    /// </summary>
    public async Task<int> RunAsync(bool forceFullBackfill, CancellationToken ct)
    {
        var startedAtUtc = DateTime.UtcNow;
        var runId = await _repository.BeginRunAsync(startedAtUtc, ct);

        // El watermark sólo avanza con corridas exitosas: si la corrida anterior tuvo errores o
        // quedó truncada, su LastSyncAtUtc no cuenta y la ventana se recalcula hacia atrás.
        var watermark = forceFullBackfill
            ? null
            : await _repository.GetLastSuccessfulSyncWatermarkUtcAsync(ct);
        var minCreatedUtc = watermark.HasValue
            ? watermark.Value.AddDays(-_options.OverlapDays)
            : startedAtUtc.AddDays(-_options.BackfillDays);

        if (watermark is null && !forceFullBackfill)
            _logger.LogWarning("No hay watermark de sincronización exitosa previa; se ejecuta backfill de {Dias} días", _options.BackfillDays);
        else
            _logger.LogInformation("Sincronizando releases creados desde {Desde:yyyy-MM-dd HH:mm} UTC en adelante", minCreatedUtc);

        var anyTruncated = false;
        var errors = 0;
        var releasesUpserted = 0;
        var definitionsOk = 0;
        var messages = new List<string>();

        using var throttle = new SemaphoreSlim(_options.MaxDegreeOfParallelism);

        foreach (var definition in _options.Definitions)
        {
            ct.ThrowIfCancellationRequested();
            await throttle.WaitAsync(ct);
            try
            {
                // Backfill por sub-ventanas: evita truncar cuando una definición tiene más
                // releases que MaxPages x PageSize en toda la ventana de retención.
                var windows = BuildWindows(minCreatedUtc, startedAtUtc);
                foreach (var (from, to) in windows)
                {
                    var result = await SyncDefinitionWindowAsync(definition, from, to, ct);
                    if (result.Truncated)
                    {
                        anyTruncated = true;
                        messages.Add($"Truncado por tope de páginas: {definition.ProjectName} def {definition.ReleaseDefinitionId} [{from:yyyy-MM-dd}..{to:yyyy-MM-dd}]");
                    }
                    releasesUpserted += result.Upserted;
                }
                definitionsOk++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogError(ex, "Error sincronizando {Project} definition {DefId}",
                    definition.ProjectName, definition.ReleaseDefinitionId);
                messages.Add($"ERROR {definition.ProjectName} def {definition.ReleaseDefinitionId}: {ex.Message}");
            }
            finally
            {
                throttle.Release();
            }
        }

        var success = errors == 0 && !anyTruncated;
        await _repository.FinishRunAsync(
            runId,
            DateTime.UtcNow,
            minCreatedUtc,
            DateTime.UtcNow,
            _options.Definitions.Count,
            definitionsOk,
            releasesUpserted,
            errors,
            anyTruncated,
            success,
            messages.Count > 0 ? string.Join(" | ", messages) : null,
            CancellationToken.None); // cerrar la corrida aunque se cancele el resto

        _logger.LogInformation(
            "Sincronización finalizada: {Upserted} releases, {Ok}/{Total} definiciones ok, {Errors} errores, truncado={Truncated}",
            releasesUpserted, definitionsOk, _options.Definitions.Count, errors, anyTruncated);

        return success ? 0 : 1;
    }

    /// <summary>
    /// Divide la ventana total en sub-ventanas de <see cref="SyncOptions.OverlapDays"/> días,
    /// para que el tope de páginas aplique por sub-ventana y no se pierdan releases antiguos
    /// en definiciones con mucho volumen (backfill completo de ~364 días).
    /// </summary>
    private List<(DateTime From, DateTime To)> BuildWindows(DateTime minCreatedUtc, DateTime maxCreatedUtc)
    {
        var stepDays = Math.Max(1, _options.OverlapDays);
        var windows = new List<(DateTime, DateTime)>();
        for (var from = minCreatedUtc; from < maxCreatedUtc; from = from.AddDays(stepDays))
            windows.Add((from, from.AddDays(stepDays) > maxCreatedUtc ? maxCreatedUtc : from.AddDays(stepDays)));

        if (windows.Count == 0)
            windows.Add((minCreatedUtc, maxCreatedUtc));

        return windows;
    }

    private async Task<(int Upserted, bool Truncated)> SyncDefinitionWindowAsync(
        AzureDefinitionRef definition, DateTime minCreatedUtc, DateTime maxCreatedUtc, CancellationToken ct)
    {
        var page = await _client.ListReleasesAsync(definition, minCreatedUtc, maxCreatedUtc, ct);

        var upserted = 0;
        foreach (var release in page.Releases)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await _repository.UpsertReleaseAsync(release, ct);
                upserted++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error guardando release {ReleaseId} de {Project} def {DefId}",
                    release.ReleaseId, definition.ProjectName, definition.ReleaseDefinitionId);
                throw;
            }
        }

        _logger.LogInformation("{Project} def {DefId}: {Count} releases sincronizados [{Desde:yyyy-MM-dd}..{Hasta:yyyy-MM-dd}]",
            definition.ProjectName, definition.ReleaseDefinitionId, upserted, minCreatedUtc, maxCreatedUtc);

        return (upserted, page.Truncated);
    }
}
