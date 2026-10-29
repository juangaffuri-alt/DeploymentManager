namespace PasadorScript.Services.Cache
{
    public interface ICacheKeyTracker
    {
        void RegisterKey(string cacheKey, string category);
        void RemoveKey(string cacheKey, string category);
        IEnumerable<string> GetKeysByCategory(string category);
        Dictionary<string, int> GetCategoryCounts();
        int TotalEntries { get; }
        int TotalCategories { get; }
        void Clear();
    }
}
