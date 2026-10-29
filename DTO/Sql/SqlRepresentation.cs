namespace PasadorScript.DTO.Sql
{
    public class SqlValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public string? DetectedTemplateType { get; set; }
        public string? ExtractedObjectName { get; set; }
        public List<string> ExtractedParameters { get; set; } = new();
        public Dictionary<string, object> ExtractedInfo { get; set; } = new();
    }

    public enum ScriptType
    {
        AlterStoredProcedure,
        AlterView,
        CreateTable,
        AlterTable,
        AlterFunction
    }
}
