using System.Globalization;

namespace AzureReleasesSync;

/// <summary>
/// Cliente de la API REST de Release (vsrm) de Azure DevOps.
/// </summary>
public interface IAzureReleaseClient
{
    /// <summary>Lista releases de una definición creados en la ventana [minCreatedUtc, maxCreatedUtc).</summary>
    Task<AzureReleasePageResult> ListReleasesAsync(
        AzureDefinitionRef definition,
        DateTime minCreatedUtc,
        DateTime maxCreatedUtc,
        CancellationToken ct);
}

/// <summary>
/// Replica el criterio del informe gerencial: $expand=artifacts,environments,approvals
/// con paginación por continuationToken y queryOrder=descending.
/// El filtro superior se aplica localmente porque la API sólo soporta minCreatedTime.
/// </summary>
public sealed class AzureReleaseClient : IAzureReleaseClient, IDisposable
{
    private const string ContinuationTokenHeader = "x-ms-continuationtoken";

    private readonly HttpClient _http;
    private readonly SyncOptions _options;

    public AzureReleaseClient(SyncOptions options)
    {
        _options = options;
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5),
        };
        var token = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($":{options.PersonalAccessToken}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
    }

    /// <summary>
    /// Lista releases de una definición creados en la ventana [minCreatedUtc, maxCreatedUtc).
    /// El filtro superior se aplica localmente porque la API sólo soporta minCreatedTime.
    /// </summary>
    public async Task<AzureReleasePageResult> ListReleasesAsync(
        AzureDefinitionRef definition,
        DateTime minCreatedUtc,
        DateTime maxCreatedUtc,
        CancellationToken ct)
    {
        var releases = new List<AzureReleaseItem>();
        var truncated = false;
        var continuationToken = (string?)null;

        var baseUrl = $"{_options.VsrmHost}/{Uri.EscapeDataString(_options.Organization)}/{Uri.EscapeDataString(definition.ProjectName)}";
        var minCreatedIso = DateTime.SpecifyKind(minCreatedUtc, DateTimeKind.Utc)
            .ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        for (var page = 1; page <= _options.MaxPagesPerDefinition; page++)
        {
            var url =
                $"{baseUrl}/_apis/release/releases?definitionId={definition.ReleaseDefinitionId}" +
                $"&$expand=artifacts,environments,approvals" +
                $"&minCreatedTime={Uri.EscapeDataString(minCreatedIso)}" +
                $"&$top={_options.PageSize}" +
                $"&queryOrder=descending" +
                $"&api-version=7.1";

            if (!string.IsNullOrEmpty(continuationToken))
                url += $"&continuationToken={Uri.EscapeDataString(continuationToken)}";

            using var response = await _http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (body.RootElement.TryGetProperty("value", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    var mapped = AzureReleaseItem.Map(definition, item);
                    if (mapped is not null && mapped.CreatedOnUtc < maxCreatedUtc)
                        releases.Add(mapped);
                }
            }

            continuationToken = ReadContinuationToken(response, body);
            if (string.IsNullOrEmpty(continuationToken))
                break;

            if (page == _options.MaxPagesPerDefinition)
                truncated = true;
        }

        return new AzureReleasePageResult(releases, truncated);
    }

    private static string? ReadContinuationToken(HttpResponseMessage response, JsonDocument body)
    {
        if (response.Headers.TryGetValues(ContinuationTokenHeader, out var values))
        {
            var headerToken = values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(headerToken))
                return headerToken;
        }

        if (body.RootElement.TryGetProperty("continuationToken", out var t) && t.ValueKind == JsonValueKind.String)
        {
            var s = t.GetString();
            return string.IsNullOrWhiteSpace(s) ? null : s;
        }

        return null;
    }

    public void Dispose() => _http.Dispose();
}

/// <summary>Página completa de releases de una definición.</summary>
public sealed record AzureReleasePageResult(IReadOnlyList<AzureReleaseItem> Releases, bool Truncated);
