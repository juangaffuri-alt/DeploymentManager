using PasadorScript.Helpers;
using System.Text.Json.Serialization;

namespace PasadorScript.DTO.AzureDev
{
    public class ReleaseWithPullRequestInfo
    {
        public int ReleaseId { get; set; }
        public string ReleaseName { get; set; }
        public string Status { get; set; }
        public string BuildId { get; set; }
        public string RepositoryId { get; set; }
        public int? PullRequestId { get; set; }
        public string PullRequestSourceBranch { get; set; }
        public string SourceCommitId { get; set; }
    }
    public class ReleaseDto
    {
        public AzureDevopsPullRequest? PullRequestObject { get; set; }
        public string AzureProjectType { get; set; }
        public int PullRequest { get; set; }
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; }
        public CreatedBy CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime ModifiedOn { get; set; }
        public string Url { get; set; }
        public List<EnvironmentDto> Environments { get; set; } = new();
        public bool ForGenexus { get; set; } = true;

        /// <summary>
        /// Gets or sets a value that indicates whether the local envío linked to the pull request is a hotfix.
        /// </summary>
        /// <value>
        /// <see langword="true" /> if the envío is a hotfix; <see langword="false" /> if it is not;
        /// <see langword="null" /> if no local envío was found for the pull request.
        /// </value>
        public bool? IsHotfix { get; set; }
        [JsonIgnore]
        public DateTime ModifiedOnArgentina => ModifiedOn.ToArgentinaTime();

        [JsonIgnore]
        public string ModifiedOnArgentinaString => ModifiedOn.ToArgentinaTimeString();
        [JsonIgnore]
        public DateTime CreatedOnArgentina => CreatedOn.ToArgentinaTime();

        [JsonIgnore]
        public string CreatedOnArgentinaString => CreatedOn.ToArgentinaTimeString();
    }

    public class Approvers
    {
        public int Id { get; set; }
        public CreatedBy Approver { get; set; }
        public CreatedBy ApprovedBy { get; set; }
        public string Status { get; set; }
        public DateTime ModifiedOn { get; set; }
    }

    public class EnvironmentDto
    {
        public int Id { get; set; }
        public bool Visible { get; set; } = true;
        public string AzureProjectType { get; set; } = "Genexus";
        public int definitionEnvironmentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusBadgeLabel => Status switch
        {
            "failed" => "badge badge-danger",
            "queued" => "badge badge-secondary",
            "inProgress" => "badge badge-primary",
            "notDeployed" => "badge badge-warning",
            "notStarted" => "badge badge-count",
            "partiallySucceeded" => "badge badge-warning",
            "succeeded" => "badge badge-success",
            "canceled" => "badge badge-danger",
            "rejected" => "badge badge-danger",
            _ => "undefined"
        };
        public string BorderClass => Status switch
        {
            "failed" => "error",
            "inProgress" => "primary",
            "queued" => "secondary",
            "notStarted" => "warning",
            "notDeployed" => "warning",
            "partiallySucceeded" => "warning",
            "succeeded" => "success",
            "canceled" => "error",
            "rejected" => "error",
            _ => "undefined"
        };
        public string StatusLabel => Status switch
        {
            "failed" => "con errores",
            "inProgress" => "en progreso",
            "queued" => "en cola",
            "notStarted" => "no iniciado",
            "notDeployed" => "no iniciado",
            "partiallySucceeded" => "parcialmente aplicado",
            "succeeded" => "aplicado",
            "canceled" => "cancelado",
            "rejected" => "rejectado",
            _ => "undefined"
        };
        public bool IsProduction => EsNombreProduccion(Name);

        public bool HasPendingApproval =>
            (PreDeployApprovals ?? [])
                .Concat(PostDeployApprovals ?? [])
                .Any(a => string.Equals(a.Status, "pending", StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// PROD, PRO, PRO (BT/BTS) y variantes. No usa Contains("PRO") para no matchear PRE.
        /// </summary>
        public static bool EsNombreProduccion(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var n = name.Trim();
            if (n.Equals("PROD", StringComparison.OrdinalIgnoreCase)
                || n.Equals("PRO", StringComparison.OrdinalIgnoreCase)
                || n.StartsWith("PROD", StringComparison.OrdinalIgnoreCase))
                return true;

            return n.StartsWith("PRO ", StringComparison.OrdinalIgnoreCase)
                || n.StartsWith("PRO(", StringComparison.OrdinalIgnoreCase)
                || n.StartsWith("PRO-", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Indica si <paramref name="status"/> significa que el deploy termino y se aplico.
        /// </summary>
        /// <param name="status">Estado del environment en Azure.</param>
        /// <returns><see langword="true"/> si el estado es <c>succeeded</c> o <c>partiallySucceeded</c>; en caso contrario, <see langword="false"/>.</returns>
        public static bool EsEstadoAplicado(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return false;

            return status.Equals("succeeded", StringComparison.OrdinalIgnoreCase)
                || status.Equals("partiallySucceeded", StringComparison.OrdinalIgnoreCase);
        }

        public List<Approvers> PreDeployApprovals { get; set; } = new();
        public List<ApprovalDto> PreApprovedDefinition { get; set; } = new();
        public List<Approvers> PostDeployApprovals { get; set; } = new();
        public List<ApprovalDto> PostApprovedDefinition { get; set; } = new();
        public DateTime? ModifiedOn { get; set; }
        public int Attempt { get; set; }
        [JsonIgnore]
        public DateTime? ModifiedOnArgentina => ModifiedOn.HasValue ? ModifiedOn.ToArgentinaTime() : null;
        [JsonIgnore]
        public string? ModifiedOnArgentinaString => ModifiedOn.HasValue ? ModifiedOn.Value.ToArgentinaTimeString() : null;
    }

    public class ApprovalDto
    {
        public string Status { get; set; } = string.Empty;
        public CreatedBy Approver { get; set; }
    }
    public enum ReleaseEnvironmentAction
    {
        Approve,
        Reject
    }

    public class ReleaseEnvironmentActionResult
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class ReleaseResponse
    {
        [JsonPropertyName("environments")]
        public List<EnvironmentDetail>? Environments { get; set; }
    }

    public class ReleaseDefinitionResponse
    {
        [JsonPropertyName("environments")]
        public List<EnvironmentDetail>? Environments { get; set; }
    }

    public class EnvironmentDetail
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    // Modelos para deserializar la respuesta
    public class ReleaseEnvironment
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class ReleaseDefinitionInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
    }


    public class ReleaseDefinitionsResponse
    {
        [JsonPropertyName("value")]
        public List<ReleaseDefinitionDetail>? Value { get; set; }
    }

    public class ReleaseDefinitionDetail
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;
    }
}
