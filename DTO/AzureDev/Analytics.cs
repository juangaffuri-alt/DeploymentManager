namespace PasadorScript.DTO.AzureDev
{
    public class PipelineAnalyticsResult
    {
        public int TotalRuns { get; set; }
        public int SuccessfulRuns { get; set; }
        public double SuccessRate => TotalRuns > 0 ? (double)SuccessfulRuns / TotalRuns * 100 : 0;
        public double AverageDurationSeconds { get; set; }
    }
}
