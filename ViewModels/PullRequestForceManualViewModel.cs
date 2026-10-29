using PasadorScript.DTO.AzureDev;

namespace PasadorScript.ViewModels
{
    public class PullRequestForceManualViewModel: AzureDevopsPullRequest
    {
        public BuildStatusResult? Build { get; set; }
        public string Owner { get; set; }
    }
}
