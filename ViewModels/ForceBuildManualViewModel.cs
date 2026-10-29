namespace PasadorScript.ViewModels
{
    public class ForceBuildManualViewModel
    {
        public int DefinitionId { get; set; }
        public int PullRequestId { get; set; }
        public string Repository { get; set; }
        public string Owner { get; set; }
    }
}
