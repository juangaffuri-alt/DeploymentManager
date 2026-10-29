using System.Text.Json;
using System.Text.RegularExpressions;
using PasadorScript.DTO.AzureDev;

namespace PasadorScript.Helpers;

/// <summary>
/// Extrae un identificador de pull request de metadatos de build de Azure DevOps o nombres de rama.
/// </summary>
public static class PullRequestIdExtractor
{
    private static readonly Regex PullRequestBranchRegex = new(
        @"refs/pull/(\d+)/",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PullRequestMessageRegex = new(
        @"(?i)(?:merged\s+|auto-completed\s+)?(?:pull\s+request|pr)\s*#?\s*(\d+)",
        RegexOptions.Compiled);

    /// <summary>
    /// Intenta extraer un identificador de pull request de un nombre de rama como <c>refs/pull/123/merge</c>.
    /// </summary>
    /// <param name="branch">Nombre de rama o ref.</param>
    /// <returns>El identificador de pull request si se encontro; en caso contrario, <see langword="null"/>.</returns>
    public static int? TryFromBranch(string? branch)
    {
        if (string.IsNullOrWhiteSpace(branch))
            return null;

        var match = PullRequestBranchRegex.Match(branch);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var prId) && prId > 0)
            return prId;

        return null;
    }

    /// <summary>
    /// Intenta extraer un identificador de pull request de un <see cref="BuildInfo"/> usando
    /// las mismas senales que <c>GetBuildByPullRequestAsync</c> (triggerInfo, parameters, sourceBranch).
    /// </summary>
    /// <param name="build">Build devuelto por Azure DevOps.</param>
    /// <returns>El identificador de pull request si se encontro; en caso contrario, <see langword="null"/>.</returns>
    public static int? TryFromBuild(BuildInfo? build)
    {
        if (build is null)
            return null;

        var fromTrigger = TryFromTriggerInfo(build.TriggerInfo);
        if (fromTrigger.HasValue)
            return fromTrigger;

        var fromParameters = TryFromParameters(build.Parameters);
        if (fromParameters.HasValue)
            return fromParameters;

        return TryFromBranch(build.SourceBranch);
    }

    private static int? TryFromTriggerInfo(IReadOnlyDictionary<string, string>? triggerInfo)
    {
        if (triggerInfo is null || triggerInfo.Count == 0)
            return null;

        if (TryParsePositiveInt(GetTriggerValue(triggerInfo, "pr.number"), out var prNumber))
            return prNumber;

        if (TryParsePositiveInt(GetTriggerValue(triggerInfo, "pullRequestId"), out var pullRequestId))
            return pullRequestId;

        if (triggerInfo.TryGetValue("ci.message", out var message) && !string.IsNullOrWhiteSpace(message))
        {
            var match = PullRequestMessageRegex.Match(message);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var fromMessage) && fromMessage > 0)
                return fromMessage;
        }

        return null;
    }

    private static int? TryFromParameters(string? parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(parameters);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in doc.RootElement.EnumerateObject())
                {
                    if (!IsPullRequestParameterName(property.Name))
                        continue;

                    if (TryParseJsonElementAsPositiveInt(property.Value, out var prId))
                        return prId;
                }
            }
        }
        catch (JsonException)
        {
            // Parameters puede no ser JSON; se continua con heuristicas de substring.
        }

        var match = PullRequestMessageRegex.Match(parameters);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var fromText) && fromText > 0)
            return fromText;

        return null;
    }

    private static string? GetTriggerValue(IReadOnlyDictionary<string, string> triggerInfo, string key)
    {
        foreach (var pair in triggerInfo)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                return pair.Value;
        }

        return null;
    }

    private static bool IsPullRequestParameterName(string name) =>
        name.Equals("system.pullRequest.pullRequestId", StringComparison.OrdinalIgnoreCase)
        || name.Equals("PullRequestId", StringComparison.OrdinalIgnoreCase)
        || name.Equals("pr.number", StringComparison.OrdinalIgnoreCase)
        || name.Equals("pullRequestId", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseJsonElementAsPositiveInt(JsonElement element, out int value)
    {
        value = 0;
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value) && value > 0)
            return true;

        if (element.ValueKind == JsonValueKind.String && TryParsePositiveInt(element.GetString(), out value))
            return true;

        return false;
    }

    private static bool TryParsePositiveInt(string? raw, out int value)
    {
        value = 0;
        return !string.IsNullOrWhiteSpace(raw) && int.TryParse(raw, out value) && value > 0;
    }
}
