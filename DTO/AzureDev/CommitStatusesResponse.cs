namespace PasadorScript.DTO.AzureDev
{
    public class CommitStatusesResponse
    {
        public string State { get; set; }
        public string Description { get; set; }
        public string TargetUrl { get; set; }
        public int? BuildId { get; set; }
    }
}
