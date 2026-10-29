using System.Collections.Concurrent;

namespace PasadorScript.Services.Cache
{
    public class CacheKeyTracker : ICacheKeyTracker
    {
        private readonly ConcurrentDictionary<string, List<string>> _cacheKeysByType;
        private readonly object _lock = new object();

        public CacheKeyTracker()
        {
            _cacheKeysByType = new ConcurrentDictionary<string, List<string>>();
        }

        public void RegisterKey(string cacheKey, string category)
        {
            _cacheKeysByType.AddOrUpdate(
                category,
                new List<string> { cacheKey },
                (key, existingList) =>
                {
                    lock (existingList)
                    {
                        if (!existingList.Contains(cacheKey))
                            existingList.Add(cacheKey);
                    }
                    return existingList;
                });
        }

        public void RemoveKey(string cacheKey, string category)
        {
            if (_cacheKeysByType.TryGetValue(category, out var keys))
            {
                lock (keys)
                {
                    keys.Remove(cacheKey);
                }
                if (keys.Count == 0)
                    _cacheKeysByType.TryRemove(category, out _);
            }
        }

        public IEnumerable<string> GetKeysByCategory(string category)
        {
            return _cacheKeysByType.TryGetValue(category, out var keys)
                ? keys.ToList()
                : Enumerable.Empty<string>();
        }

        public Dictionary<string, int> GetCategoryCounts()
        {
            return _cacheKeysByType.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count);
        }

        public int TotalEntries => _cacheKeysByType.Sum(kvp => kvp.Value.Count);
        public int TotalCategories => _cacheKeysByType.Count;

        public void Clear()
        {
            _cacheKeysByType.Clear();
        }
    }
}
