using System.Globalization;
using System.Text.Json;

namespace AzureReleasesSync.Models;

/// <summary>
/// Release mapeado desde el payload de la API (equivalente a ReleaseListItemDto del informe gerencial,
/// pero conservando el JSON crudo por release como salvaguarda de auditoría).
/// </summary>
public sealed class AzureReleaseItem
{
    public required string ProjectName { get; init; }
    public int ReleaseDefinitionId { get; init; }
    public int ReleaseId { get; init; }
    public string? Name { get; init; }
    public string? Status { get; init; }
    public DateTime CreatedOnUtc { get; init; }
    public DateTime? ModifiedOnUtc { get; init; }
    public int? PullRequestId { get; set; }
    public int? BuildId { get; init; }
    public Guid? RepositoryId { get; init; }
    public string? BuildNumber { get; init; }
    public string? ReleaseUrl { get; init; }
    public string? Description { get; init; }
    public string RawJson { get; init; } = "{}";
    public List<AzureEnvironmentItem> Environments { get; init; } = [];

    public static AzureReleaseItem? Map(AzureDefinitionRef definition, JsonElement item)
    {
        if (!item.TryGetProperty("id", out var idEl) || !int.TryParse(idEl.ToString(), out var releaseId) || releaseId <= 0)
            return null;

        var environments = new List<AzureEnvironmentItem>();
        if (item.TryGetProperty("environments", out var envs) && envs.ValueKind == JsonValueKind.Array)
            environments.AddRange(envs.EnumerateArray().Select(AzureEnvironmentItem.Map).OfType<AzureEnvironmentItem>());

        // Aprobaciones anidadas en la raíz del release: se asignan al environment correspondiente
        // si el environment no trajo sus propias PRE/POST (mismo criterio que ApplyRootApprovals).
        if (item.TryGetProperty("approvals", out var rootApprovals) && rootApprovals.ValueKind == JsonValueKind.Array)
            ApplyRootApprovals(environments, rootApprovals);

        Guid? repositoryId = null;
        int? pullRequestId = null;
        int? buildId = null;
        string? buildNumber = null;

        if (item.TryGetProperty("artifacts", out var artifacts) && artifacts.ValueKind == JsonValueKind.Array)
        {
            foreach (var artifact in artifacts.EnumerateArray())
            {
                var type = GetString(artifact, "type");
                if (!artifact.TryGetProperty("definitionReference", out var defRef))
                    continue;

                if (string.Equals(type, "GitBuild", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryGetInt(defRef, ["pullRequest", "id"], out var pr))
                        pullRequestId = pr;
                    if (Guid.TryParse(GetString(defRef, "repository", "id"), out var repo))
                        repositoryId = repo;
                }

                if (string.Equals(type, "Build", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(type, "GitBuild", StringComparison.OrdinalIgnoreCase))
                {
                    if (buildId is null && TryGetInt(defRef, ["build", "id"], out var b))
                        buildId = b;

                    buildNumber ??= GetString(defRef, "build", "number");
                }
            }

            // En GitBuild el PR suele venir también en properties.pullRequest
            if (pullRequestId is null)
            {
                foreach (var artifact in artifacts.EnumerateArray())
                {
                    if (artifact.TryGetProperty("properties", out var props)
                        && props.TryGetProperty("pullRequest", out var prProp)
                        && int.TryParse(prProp.ToString(), out var pr2))
                    {
                        pullRequestId = pr2;
                        break;
                    }
                }
            }
        }

        return new AzureReleaseItem
        {
            ProjectName = definition.ProjectName,
            ReleaseDefinitionId = definition.ReleaseDefinitionId,
            ReleaseId = releaseId,
            Name = GetString(item, "name"),
            Status = GetString(item, "status"),
            CreatedOnUtc = GetDate(item, "createdOn") ?? DateTime.MinValue,
            ModifiedOnUtc = GetDate(item, "modifiedOn"),
            PullRequestId = pullRequestId,
            BuildId = buildId,
            RepositoryId = repositoryId,
            BuildNumber = buildNumber,
            ReleaseUrl = GetString(item, "_links", "web", "href"),
            Description = GetString(item, "description"),
            RawJson = item.GetRawText(),
            Environments = environments,
        };
    }

    private static void ApplyRootApprovals(List<AzureEnvironmentItem> environments, JsonElement rootApprovals)
    {
        foreach (var env in environments)
        {
            if (env.PreDeployApprovals.Count > 0 && env.PostDeployApprovals.Count > 0)
                continue;

            var matching = rootApprovals.EnumerateArray()
                .Where(a => BelongsToEnvironment(a, env))
                .ToList();
            if (matching.Count == 0)
                continue;

            if (env.PreDeployApprovals.Count == 0)
            {
                var mapped = matching
                    .Where(IsPreDeployApproval)
                    .Select(AzureApprovalItem.Map)
                    .OfType<AzureApprovalItem>()
                    .ToList();
                if (mapped.Count > 0)
                    env.PreDeployApprovals = mapped;
            }

            if (env.PostDeployApprovals.Count == 0)
            {
                var mapped = matching
                    .Where(IsPostDeployApproval)
                    .Select(AzureApprovalItem.Map)
                    .OfType<AzureApprovalItem>()
                    .ToList();
                if (mapped.Count > 0)
                    env.PostDeployApprovals = mapped;
            }
        }
    }

    private static bool BelongsToEnvironment(JsonElement approval, AzureEnvironmentItem env)
    {
        if (!approval.TryGetProperty("releaseEnvironment", out var relEnv))
            return false;

        var name = GetString(relEnv, "name");
        if (!string.IsNullOrWhiteSpace(name) && string.Equals(name.Trim(), env.Name, StringComparison.OrdinalIgnoreCase))
            return true;

        return relEnv.TryGetProperty("id", out var idEl)
            && int.TryParse(idEl.ToString(), out var id)
            && id == env.EnvironmentId && env.EnvironmentId != 0;
    }

    private static bool IsPreDeployApproval(JsonElement approval) =>
        string.Equals(GetString(approval, "approvalType"), "preDeploy", StringComparison.OrdinalIgnoreCase);

    private static bool IsPostDeployApproval(JsonElement approval)
    {
        var type = GetString(approval, "approvalType");
        return string.Equals(type, "afterDeploy", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "postDeploy", StringComparison.OrdinalIgnoreCase);
    }

    internal static string? GetString(JsonElement el, params string[] path)
    {
        var current = el;
        foreach (var segment in path)
        {
            if (!current.TryGetProperty(segment, out current))
                return null;
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => current.ToString(),
        };
    }

    internal static bool TryGetInt(JsonElement el, string[] path, out int value)
    {
        value = 0;
        var s = GetString(el, path);
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    internal static DateTime? GetDate(JsonElement el, string property)
    {
        var s = GetString(el, property);
        if (string.IsNullOrWhiteSpace(s))
            return null;
        return DateTime.TryParse(s, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt)
            ? dt
            : null;
    }
}

/// <summary>Environment (stage) de un release.</summary>
public sealed class AzureEnvironmentItem
{
    public int EnvironmentId { get; init; }
    public required string Name { get; init; }
    public int Rank { get; init; }
    public string Status { get; init; } = string.Empty;
    public int Attempt { get; init; }
    public DateTime? QueuedOnUtc { get; init; }
    public DateTime? StartedOnUtc { get; init; }
    public DateTime? CompletedOnUtc { get; init; }
    public string? DeployedByDisplayName { get; init; }
    public string RawJson { get; init; } = "{}";
    public List<AzureApprovalItem> PreDeployApprovals { get; set; } = [];
    public List<AzureApprovalItem> PostDeployApprovals { get; set; } = [];

    public static AzureEnvironmentItem? Map(JsonElement env)
    {
        var name = AzureReleaseItem.GetString(env, "name")?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var deploySteps = env.TryGetProperty("deploySteps", out var ds) && ds.ValueKind == JsonValueKind.Array
            ? ds.EnumerateArray().ToList()
            : [];
        var lastDeploy = deploySteps.Count > 0 ? deploySteps[^1] : (JsonElement?)null;

        return new AzureEnvironmentItem
        {
            EnvironmentId = ReadInt(env, "id"),
            Name = name,
            Rank = ReadInt(env, "rank"),
            Status = AzureReleaseItem.GetString(env, "status") ?? string.Empty,
            Attempt = lastDeploy.HasValue ? ReadInt(lastDeploy.Value, "attempt") : ReadInt(env, "attempt"),
            QueuedOnUtc = lastDeploy.HasValue ? AzureReleaseItem.GetDate(lastDeploy.Value, "queuedOn") : null,
            StartedOnUtc = lastDeploy.HasValue ? AzureReleaseItem.GetDate(lastDeploy.Value, "startedOn") : null,
            CompletedOnUtc = ResolveCompletedOn(deploySteps),
            DeployedByDisplayName =
                AzureReleaseItem.GetString(env, "lastModifiedBy", "displayName")
                ?? AzureReleaseItem.GetString(env, "createdBy", "displayName"),
            RawJson = env.GetRawText(),
            PreDeployApprovals = MapApprovals(env, "preDeployApprovals"),
            PostDeployApprovals = MapApprovals(env, "postDeployApprovals"),
        };
    }

    /// <summary>
    /// Fin del último deploy step aplicado (mismo criterio que el informe: no usa modifiedOn de un rechazo).
    /// </summary>
    private static DateTime? ResolveCompletedOn(List<JsonElement> deploySteps)
    {
        for (var i = deploySteps.Count - 1; i >= 0; i--)
        {
            var step = deploySteps[i];
            var status = AzureReleaseItem.GetString(step, "deployStep", "operation", "result", "status");
            if (string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "partiallySucceeded", StringComparison.OrdinalIgnoreCase))
            {
                return AzureReleaseItem.GetDate(step, "completedOn");
            }
        }

        return null;
    }

    private static List<AzureApprovalItem> MapApprovals(JsonElement env, string property)
    {
        if (!env.TryGetProperty(property, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return [];

        return [.. arr.EnumerateArray().Select(AzureApprovalItem.Map).OfType<AzureApprovalItem>()];
    }

    private static int ReadInt(JsonElement el, string property)
    {
        var s = AzureReleaseItem.GetString(el, property);
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }
}

/// <summary>Aprobación PRE/POST de un environment.</summary>
public sealed class AzureApprovalItem
{
    public int ApprovalId { get; init; }
    public int Rank { get; init; }
    public int Attempt { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool IsAutomated { get; init; }
    public string? ApproverId { get; init; }
    public string? ApproverDisplayName { get; init; }
    public string? ApprovedByDisplayName { get; init; }
    public string? Comment { get; init; }
    public DateTime? ModifiedOnUtc { get; init; }
    public string RawJson { get; init; } = "{}";

    public static AzureApprovalItem? Map(JsonElement approval)
    {
        var status = AzureReleaseItem.GetString(approval, "status");
        if (status is null)
            return null;

        AzureReleaseItem.TryGetInt(approval, ["id"], out var parsed);

        return new AzureApprovalItem
        {
            ApprovalId = parsed,
            Rank = ReadInt(approval, "rank"),
            Attempt = ReadInt(approval, "attempt"),
            Status = status,
            IsAutomated = ReadBool(approval, "isAutomatedInParallelRelease"),
            ApproverId =
                AzureReleaseItem.GetString(approval, "approver", "uniqueName")
                ?? AzureReleaseItem.GetString(approval, "approver", "id"),
            ApproverDisplayName = AzureReleaseItem.GetString(approval, "approver", "displayName"),
            ApprovedByDisplayName = AzureReleaseItem.GetString(approval, "approvedBy", "displayName"),
            Comment = AzureReleaseItem.GetString(approval, "comment"),
            ModifiedOnUtc = AzureReleaseItem.GetDate(approval, "modifiedOn"),
            RawJson = approval.GetRawText(),
        };
    }

    private static int ReadInt(JsonElement el, string property)
    {
        var s = AzureReleaseItem.GetString(el, property);
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private static bool ReadBool(JsonElement el, string property)
    {
        if (!el.TryGetProperty(property, out var v))
            return false;
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(v.GetString(), out var b) && b,
            _ => false,
        };
    }
}
