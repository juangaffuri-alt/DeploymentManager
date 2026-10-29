using PasadorScript.DTO.AzureDev;

namespace PasadorScript.ViewModels
{
    public class DashBoardViewModel
    {
        public string? StatusFilterLabel { get; set; }
        public ICollection<OrdenBuildViewModel>? PullRequestGx { get; set; }
        public PipelineAnalyticsResult? GxAnalytics { get; set; }
        public ICollection<AgentPool>? AgentPools { get; set; }
        public ICollection<OrdenBuildViewModel> PullRequestScripts { get; set; }
        public string? StatusFilterLabelScripts { get; set; }
        public PipelineAnalyticsResult? ScriptsAnalytics { get; set; }

    }
}
