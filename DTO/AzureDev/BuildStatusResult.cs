
using PasadorScript.Helpers;
using System.Text.Json.Serialization;

namespace PasadorScript.DTO.AzureDev
{
    public class BuildStatusResult
    {
        public int? DefinitionId { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }

        // Build Information
        public int BuildId { get; set; }
        public string? Status { get; set; }
        public string? Result { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? FinishTime { get; set; }
        public string? BuildNumber { get; set; }
        public string? SourceBranch { get; set; }
        public string? WebUrl { get; set; }

        // Pull Request Information
        public int PullRequestId { get; set; }
        public string? PullRequestStatus { get; set; }
        public string? PullRequestTitle { get; set; }
        public string? SourceRefName { get; set; }
        public string? TargetRefName { get; set; }

        // Build Status Properties
        public bool IsCompleted => Status?.Equals("completed", StringComparison.OrdinalIgnoreCase) == true;
        public bool IsSuccessful => Result?.Equals("succeeded", StringComparison.OrdinalIgnoreCase) == true;
        public bool IsFailed => Result?.Equals("failed", StringComparison.OrdinalIgnoreCase) == true;
        public bool IsInProgress => Status?.Equals("inProgress", StringComparison.OrdinalIgnoreCase) == true;

        // Pull Request Status Properties
        public bool IsPullRequestCompleted => PullRequestStatus?.Equals("completed", StringComparison.OrdinalIgnoreCase) == true;
        public bool IsPullRequestActive => PullRequestStatus?.Equals("active", StringComparison.OrdinalIgnoreCase) == true;
        public bool IsPullRequestAbandoned => PullRequestStatus?.Equals("abandoned", StringComparison.OrdinalIgnoreCase) == true;

        //Badges
        public string PrStatusBadgeClass => PullRequestStatus switch
        {
            "active" => "badge badge-primary",
            "completed" => "badge badge-success",
            "abandoned" => "badge badge-danger",
            "canceled" => "badge badge-danger",
            "inProgress" => "badge badge-primary",
            _ => ""
        };

        public string BuilStatusBadgeClass => Result switch
        {
            "notStarted" => "badge badge-count",
            "succeeded" => "badge badge-success",
            "failed" => "badge badge-danger",
            "canceled" => "badge badge-danger",
            "inProgress" => "badge badge-primary",
            "completed" => "badge badge-success",
            _ => ""
        };
        [JsonIgnore]
        public DateTime? StartTimeArgentina => StartTime.HasValue ? StartTime.ToArgentinaTime() : null;
        [JsonIgnore]
        public string? StartTimeArgentinaString => StartTime.HasValue ? StartTime.Value.ToArgentinaTimeString() : null;
        [JsonIgnore]
        public DateTime? FinishTimeArgentina => FinishTime.HasValue ? FinishTime.ToArgentinaTime() : null;
        [JsonIgnore]
        public string? FinishTimeArgentinaString => FinishTime.HasValue ? FinishTime.Value.ToArgentinaTimeString() : null;
    }

}
