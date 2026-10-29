namespace PasadorScript.DTO.AzureDev
{
    public class BuildInfo
    {
        public Definition Definition { get; set; }
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Result { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? FinishTime { get; set; }
        public int? DefinitionId { get; set; } = null;
        public string SourceVersion { get; set; } = ""; // Commit ID
        public string SourceBranch { get; set; } = string.Empty;
        public string? Parameters { get; set; }
        public string? Reason { get; set; }
        public Dictionary<string, string>? TriggerInfo { get; set; }
    }

        
    public class BuildsResponse
    {
        public List<BuildInfo> Value { get; set; }
        public int Count { get; set; }
    }
    // Clases para deserializar la respuesta JSON
    public class LogsResponse
    {
        public int Count { get; set; }
        public LogEntry[] Value { get; set; }
    }

    public class LogEntry
    {
        public int Id { get; set; }
        public string Type { get; set; }
        public string Url { get; set; }
    }
    public class BuildTriggerRequest
    {
        public Definition Definition { get; set; }
        public string SourceBranch { get; set; }
        public string Parameters { get; set; }
    }

    public class Definition
    {
        public int Id { get; set; }
    }
}
