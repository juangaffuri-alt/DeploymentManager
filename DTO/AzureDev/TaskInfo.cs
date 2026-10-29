namespace PasadorScript.DTO.AzureDev
{
    public class TaskInfo
    {
        public string Id { get; set; }
        public string ParentId { get; set; }
        public string Name { get; set; }
        public int? LogId { get; set; }
        public string Type { get; set; }
        public string State { get; set; }
        public string Result { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? FinishTime { get; set; }
    }
}
