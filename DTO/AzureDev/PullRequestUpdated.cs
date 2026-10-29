namespace PasadorScript.DTO.AzureDev
{
    public class PullRequestUpdated
    {
        public int PullRequestId { get; set; }
        public int NewPullRequestId { get; set; }
        public string BranchName { get; set; }
    }
}
