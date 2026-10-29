namespace PasadorScript.Models
{
    public class CacheSettings
    {
        public DevOpsCacheSettings DevOps { get; set; } = new DevOpsCacheSettings();
    }

    public class DevOpsCacheSettings
    {
        public TimeSpan ActiveStatusDuration { get; set; } = TimeSpan.FromMinutes(1);
        public TimeSpan FinalStatusDuration { get; set; } = TimeSpan.FromHours(1);
        public TimeSpan DefaultDuration { get; set; } = TimeSpan.FromMinutes(5);
        public int MaxCacheSize { get; set; } = 100; // MB
    }
    public class CacheStatistics
    {
        public int TotalEntries { get; set; }
        public int TotalCategories { get; set; }
        public Dictionary<string, int> CategoryCounts { get; set; } = new Dictionary<string, int>();
    }
}
