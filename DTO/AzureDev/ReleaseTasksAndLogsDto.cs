namespace PasadorScript.DTO.AzureDev
{
    public class ReleaseTasksAndLogsDto
    {
        public int ReleaseId { get; set; }
        public string ReleaseName { get; set; } = string.Empty;
        public int EnvironmentId { get; set; }
        public string EnvironmentName { get; set; } = string.Empty;
        public List<ReleaseTaskDto> Tasks { get; set; } = new List<ReleaseTaskDto>();
    }

    public class ReleaseTaskDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusLabel => Status switch
        {
            "succeeded" => "Exitoso",
            "failed" => "Fallido",
            "inProgress" => "En Progreso",
            "pending" => "Pendiente",
            "canceled" => "Cancelado",
            "skipped" => "Omitido",
            _ => Status
        };
        public string StatusBadgeClass => Status switch
        {
            "succeeded" => "badge bg-success",
            "failed" => "badge bg-danger",
            "inProgress" => "badge bg-primary",
            "pending" => "badge bg-warning",
            "canceled" => "badge bg-secondary",
            "skipped" => "badge bg-light text-dark",
            _ => "badge bg-light text-dark"
        };
        public string StatusIconClass => Status switch
        {
            "succeeded" => "fa fa-check-circle text-success",
            "failed" => "fa fa-times-circle text-danger",
            "inProgress" => "fa fa-spinner fa-spin text-primary",
            "pending" => "fa fa-clock text-warning",
            "canceled" => "fa fa-ban text-secondary",
            "skipped" => "fa fa-forward text-muted",
            _ => "fa fa-question-circle text-muted"
        };
        public DateTime? StartTime { get; set; }
        public DateTime? FinishTime { get; set; }
        public TimeSpan? Duration => StartTime.HasValue && FinishTime.HasValue
            ? FinishTime.Value - StartTime.Value
            : null;
        public string DurationText => Duration?.ToString(@"mm\:ss") ?? "--";
        public int Order { get; set; }
        public bool HasLog { get; set; }
    }
}
