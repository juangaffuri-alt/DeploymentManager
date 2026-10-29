using PasadorScript.DbModels;
using PasadorScript.DTO.AzureDev;

namespace PasadorScript.ViewModels
{
    public class OrdenBuildViewModel
    {
        public AzureDevopsPullRequest PullRequest { get; set; }
        public BuildGxViewWithClasses? BuildModel { get; set; }
    }
}
