using Microsoft.AspNetCore.Http.HttpResults;
using PasadorScript.DTO.AzureDev;
using PasadorScript.Helpers;
using System.Security.Cryptography.Pkcs;
using System.Text.Json.Serialization;

namespace PasadorScript.DTO.Auditoria
{
    // DTOs para la vista de auditoría
    public class ReleaseAuditDto
    {
        public int ReleaseId { get; set; }
        public string ReleaseName { get; set; } = "";
        public DateTime CreatedOn { get; set; }
        [JsonIgnore]
        public DateTime CreatedOnArgentina => CreatedOn.ToArgentinaTime();

        [JsonIgnore]
        public string CreatedOnArgentinaString => CreatedOn.ToArgentinaTimeString();

        // Pull Request Info
        public PullRequestAuditInfo PullRequest { get; set; }

        // Environments con sus deployments y approvals
        public List<EnvironmentAuditDto> Environments { get; set; } = new List<EnvironmentAuditDto>();

        /// <summary>
        /// Último environment que se desplegó. Si nadie arrancó, el primer fallido/rechazado/cancelado.
        /// No usa el último de la definición: en este pipeline suele ser PRO aún sin deploy.
        /// </summary>
        [JsonIgnore]
        public EnvironmentAuditDto? EnvironmentDestacado
        {
            get
            {
                if (Environments.Count == 0)
                    return null;

                return Environments.LastOrDefault(e =>
                        e.Deployment.StartedOn.HasValue && !e.EstaPendienteDeInicio)
                    ?? Environments.FirstOrDefault(e => e.EsResultadoFallido)
                    ?? Environments[0];
            }
        }
    }

    public class PullRequestAuditInfo
    {
        public int PullRequestId { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public CreatedBy CreatedBy { get; set; } = new();
        public DateTime CreationDate { get; set; }
        public DateTime? ClosedDate { get; set; }
        public string Status { get; set; } = "";
        [JsonIgnore]
        public DateTime CreationDateArgentina => CreationDate.ToArgentinaTime();
        [JsonIgnore]
        public string CreationDateArgentinaString => CreationDate.ToArgentinaTimeString();
        [JsonIgnore]
        public DateTime? ClosedOnArgentina => ClosedDate.HasValue ? ClosedDate.ToArgentinaTime() : null;
        [JsonIgnore]
        public string? ClosedOnArgentinaString => ClosedDate.HasValue ? ClosedDate.Value.ToArgentinaTimeString() : null;

        // Commits
        public List<CommitInfo> Commits { get; set; } = new List<CommitInfo>();

        // Reviewers/Approvers del PR
        public List<ReviewerInfo> Reviewers { get; set; } = new List<ReviewerInfo>();
        public List<ThreadInfo> Threads { get; set; } = new();
        public List<CommentInfo> ComentariosNoAsociados { get; set; } = new();

        /// <summary>
        /// Tono del marcador: el peor voto manda (rechazo > espera/sugerencias > aprobado).
        /// </summary>
        [JsonIgnore]
        public string ReviewsMarkerClass
        {
            get
            {
                if (Reviewers.Count == 0)
                    return "bg-secondary";
                if (Reviewers.Any(r => r.VoteTone == "danger"))
                    return "bg-danger";
                if (Reviewers.Any(r => r.VoteTone == "warning"))
                    return "bg-warning";
                if (Reviewers.Any(r => r.VoteTone == "success"))
                    return "bg-success";
                return "bg-secondary";
            }
        }
    }

    public class ThreadInfo
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty; // active, fixed, etc.
        public string ThreadContext { get; set; } = string.Empty; // archivo o general
        public Dictionary<string, object>? Properties { get; set; }
        public List<CommentInfo> Comments { get; set; } = new();
    }

    public class CommentInfo
    {
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public string CommentType { get; set; } = string.Empty;
        public CreatedBy Author { get; set; } = new();
        public DateTime PublishedDate { get; set; }
        public DateTime LastUpdatedDate { get; set; }
        public bool IsDeleted { get; set; }
        public int? ParentCommentId { get; set; }
        public string ThreadContext { get; set; } = "general";

        public DateTime PublishedDateArgentina => PublishedDate.ToArgentinaTime();

        public DateTime LastUpdatedDateArgentina => LastUpdatedDate.ToArgentinaTime();
    }
    public class VotoInfo
    {
        public string VoterIdentity { get; set; } = "";
        public string VoteResult { get; set; } = "";
        public string Comment { get; set; } = "";
        public string CommentDate { get; set; } = "";
    }

    public class CommitInfo
    {
        public string CommitId { get; set; } = "";
        public string Comment { get; set; } = "";
        public CreatedBy Author { get; set; } = new();
        public DateTime Date { get; set; }
        [JsonIgnore]
        public DateTime DateArgentina => Date.ToArgentinaTime();

        [JsonIgnore]
        public string DateArgentinaString => Date.ToArgentinaTimeString();
    }

    public class ReviewerInfo
    {
        public CreatedBy Reviewer { get; set; } = new();
        public string Vote { get; set; } = ""; // "approved", "approved-with-suggestions", "rejected", etc.
        public DateTime? VotedOn { get; set; }

        public string? Comment { get; set; }
        public List<CommentInfo> Comments { get; set; } = new List<CommentInfo>();

        [JsonIgnore]
        public DateTime? VotedOnArgentina => VotedOn.ToArgentinaTime();

        private string VoteKey => (Vote ?? "").Trim().ToLowerInvariant().Replace(' ', '-');

        [JsonIgnore]
        public string VoteLabel => VoteKey switch
        {
            "approved" => "Aprobado",
            "approved-with-suggestions" => "Aprobado con sugerencias",
            "rejected" => "Rechazado",
            "waiting-for-author" => "Esperando autor",
            "no-vote" => "Sin voto",
            _ => "Sin voto"
        };

        [JsonIgnore]
        public string VoteBadgeClass => VoteKey switch
        {
            "approved" => "badge badge-success",
            "approved-with-suggestions" => "badge badge-warning",
            "rejected" => "badge badge-danger",
            "waiting-for-author" => "badge badge-black",
            _ => "badge badge-black"
        };

        [JsonIgnore]
        public string VoteIcon => VoteKey switch
        {
            "approved" => "fa-check-circle",
            "rejected" => "fa-times-circle",
            "approved-with-suggestions" => "fa-exclamation-circle",
            "waiting-for-author" => "fa-clock",
            _ => "fa-minus-circle"
        };

        [JsonIgnore]
        public string VoteTone => VoteKey switch
        {
            "approved" => "success",
            "approved-with-suggestions" => "warning",
            "rejected" => "danger",
            "waiting-for-author" => "warning",
            _ => "secondary"
        };
    }

    public class EnvironmentAuditDto
    {
        public int EnvironmentId { get; set; }
        public string Name { get; set; } = "";
        public int DefinitionEnvironmentId { get; set; }
        public int Attempt { get; set; }
        public string Status { get; set; } = "";

        [JsonIgnore]
        public bool EstaPendienteDeInicio
        {
            get
            {
                var status = (Status ?? "").Trim();
                return status.Equals("notStarted", StringComparison.OrdinalIgnoreCase)
                    || status.Equals("notDeployed", StringComparison.OrdinalIgnoreCase);
            }
        }

        [JsonIgnore]
        public bool EsResultadoFallido
        {
            get
            {
                var status = (Status ?? "").Trim();
                return status.Equals("failed", StringComparison.OrdinalIgnoreCase)
                    || status.Equals("rejected", StringComparison.OrdinalIgnoreCase)
                    || status.Equals("canceled", StringComparison.OrdinalIgnoreCase);
            }
        }

        [JsonIgnore]
        public bool IsProduction => EnvironmentDto.EsNombreProduccion(Name);

        [JsonIgnore]
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
            _ => "badge badge-secondary"
        };

        [JsonIgnore]
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
            _ => Status
        };

        [JsonIgnore]
        public string StatusTone => Status switch
        {
            "failed" or "canceled" or "rejected" => "danger",
            "succeeded" => "success",
            "inProgress" => "primary",
            "partiallySucceeded" or "notDeployed" or "notStarted" => "warning",
            _ => "secondary"
        };

        [JsonIgnore]
        public string StatusMarkerClass => $"bg-{StatusTone}";

        [JsonIgnore]
        public string StatusJumpClass => StatusTone switch
        {
            "success" => "audit-jump-destacado is-success",
            "danger" => "audit-jump-destacado is-danger",
            "warning" => "audit-jump-destacado is-warning",
            "primary" => "audit-jump-destacado is-primary",
            _ => "audit-jump-destacado is-secondary"
        };

        [JsonIgnore]
        public string? StatusCardClass =>
            StatusTone is "danger" or "warning" ? $"is-{StatusTone}" : null;

        [JsonIgnore]
        public ApprovalAuditInfo? AprobacionEvidencia
        {
            get
            {
                var relevantes = PostDeployApprovalsRelevantes
                    .Concat(PreDeployApprovalsRelevantes)
                    .ToList();

                return relevantes.LastOrDefault(a =>
                        a.TieneAprobadorHumano && !string.IsNullOrWhiteSpace(a.Comments))
                    ?? relevantes.LastOrDefault(a => a.TieneAprobadorHumano)
                    ?? relevantes.LastOrDefault();
            }
        }

        // Deployment info
        public DeploymentInfo Deployment { get; set; } = new(); 

        // Approvals
        public List<ApprovalAuditInfo> PreDeployApprovals { get; set; } = new List<ApprovalAuditInfo>();
        public List<ApprovalAuditInfo> PostDeployApprovals { get; set; } = new List<ApprovalAuditInfo>();

        [JsonIgnore]
        public IEnumerable<ApprovalAuditInfo> PreDeployApprovalsRelevantes =>
            PreDeployApprovals.Where(a => a.EsRelevante);

        [JsonIgnore]
        public IEnumerable<ApprovalAuditInfo> PostDeployApprovalsRelevantes =>
            PostDeployApprovals.Where(a => a.EsRelevante);
    }

    public class DeploymentInfo
    {
        public DateTime? StartedOn { get; set; }
        public DateTime? CompletedOn { get; set; }
        public CreatedBy RequestedBy { get; set; } = new();
        public CreatedBy DeployedBy { get; set; } = new();
        public string Reason { get; set; } = "";
        public TimeSpan? Duration { get; set; }
        [JsonIgnore]
        public DateTime? StartedOnArgentina => StartedOn.HasValue ? StartedOn.Value.ToArgentinaTime() : null;
        [JsonIgnore]
        public string? StartedOnArgentinaString => StartedOn.HasValue ? StartedOn.Value.ToArgentinaTimeString() : null;
        [JsonIgnore]
        public DateTime? CompletedOnArgentina => CompletedOn.HasValue ? CompletedOn.Value.ToArgentinaTime() : null;

        [JsonIgnore]
        public string? CompletedOnArgentinaString => CompletedOn.HasValue ? CompletedOn.Value.ToArgentinaTimeString() : null;
    }

    public class ApprovalAuditInfo
    {
        public int ApprovalId { get; set; }
        public CreatedBy ApprovalFor { get; set; } = new(); // El grupo/usuario que debe aprobar
        public CreatedBy ApprovedBy { get; set; } = new(); // Quien realmente aprobó
        public string Status { get; set; } = ""; // "approved", "rejected", "pending", "canceled"
        public DateTime CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public string Comments { get; set; } = "";
        public bool IsAutomated { get; set; }

        [JsonIgnore]
        public bool EsRelevante =>
            !IsAutomated
            && !string.Equals(Status, "skipped", StringComparison.OrdinalIgnoreCase)
            && (!string.IsNullOrWhiteSpace(ApprovalFor?.Id)
                || !string.IsNullOrWhiteSpace(ApprovalFor?.DisplayName));

        [JsonIgnore]
        public bool TieneAprobadorHumano =>
            !string.IsNullOrWhiteSpace(ApprovedBy?.DisplayName)
            || !string.IsNullOrWhiteSpace(ApprovedBy?.Id);

        [JsonIgnore]
        public string StatusLabel => Status switch
        {
            "approved" => "Aprobado",
            "rejected" => "Rechazado",
            "pending" => "Pendiente",
            "canceled" => "Cancelado",
            _ => Status
        };

        [JsonIgnore]
        public string StatusBadgeClass => Status switch
        {
            "approved" => "badge badge-success",
            "rejected" => "badge badge-danger",
            "pending" => "badge badge-warning",
            "canceled" => "badge badge-secondary",
            _ => "badge badge-secondary"
        };

        [JsonIgnore]
        public string StatusIcon => Status switch
        {
            "approved" => "fa-check-circle",
            "rejected" => "fa-times-circle",
            "pending" => "fa-clock",
            "canceled" => "fa-ban",
            _ => "fa-question-circle"
        };

        [JsonIgnore]
        public string StatusTone => Status switch
        {
            "approved" => "success",
            "rejected" => "danger",
            "pending" => "warning",
            "canceled" => "secondary",
            _ => "secondary"
        };

        [JsonIgnore]
        public string FirmanteVacioLabel => Status switch
        {
            "pending" => "Pendiente de aprobación",
            "rejected" => "Sin firmante",
            "canceled" => "Sin firmante",
            _ => "Sin firmante"
        };

        [JsonIgnore]
        public DateTime? ModifiedOnArgentina => ModifiedOn.HasValue ? ModifiedOn.Value.ToArgentinaTime() : null;

        [JsonIgnore]
        public string? ModifiedOnArgentinaString => ModifiedOn.HasValue ? ModifiedOn.Value.ToArgentinaTimeString() : null;
        [JsonIgnore]
        public DateTime CreatedOnArgentina => CreatedOn.ToArgentinaTime();

        [JsonIgnore]
        public string CreatedOnArgentinaString => CreatedOn.ToArgentinaTimeString();
    }
}
