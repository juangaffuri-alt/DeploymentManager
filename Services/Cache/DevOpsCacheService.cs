// Services/Cache/DevOpsCacheService.cs (versión corregida y unificada)
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PasadorScript.DTO.AzureDev;
using PasadorScript.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PasadorScript.Services.Cache
{
    public class DevOpsCacheService : IDevOpsCacheService
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<DevOpsCacheService> _logger;
        private readonly DevOpsCacheSettings _settings;
        private readonly ICacheKeyTracker _keyTracker; // tracker singleton

        public string Prefix { get; set; } = "";

        public DevOpsCacheService(
            IMemoryCache memoryCache,
            ILogger<DevOpsCacheService> logger,
            IOptions<CacheSettings> cacheSettings,
            ICacheKeyTracker keyTracker) // inyectamos el tracker
        {
            _memoryCache = memoryCache;
            _logger = logger;
            _settings = cacheSettings.Value.DevOps;
            _keyTracker = keyTracker;
        }

        // Métodos auxiliares para determinar estados
        private bool IsActiveStatus(string status)
        {
            if (string.IsNullOrEmpty(status)) return false;
            var statusLower = status.ToLower();
            var activeStatuses = new[]
            {
                "queued", "inprogress", "inProgress", "active",
                "pending", "notstarted", "running", "waiting",
                "starting", "initializing", "rejected"
            };
            return activeStatuses.Contains(statusLower);
        }

        private bool IsFinalStatus(string status)
        {
            if (string.IsNullOrEmpty(status)) return false;
            var statusLower = status.ToLower();
            var finalStatuses = new[]
            {
                "completed", "succeeded", "failed", "canceled", "abandoned", "merged", "stopped",
                "partiallysucceeded", "skipped", "timedout"
            };
            return finalStatuses.Contains(statusLower);
        }

        private TimeSpan GetCacheDurationForValue<T>(T value, CacheDuration defaultDuration)
        {
            if (value == null)
                return TimeSpan.FromSeconds(5);

            if (value is BuildStatusResult buildStatus)
            {
                var prStatus = buildStatus.PullRequestStatus?.ToLower() ?? "";
                var buildStatusLower = buildStatus.Status?.ToLower() ?? "";
                var buildResult = buildStatus.Result?.ToLower() ?? "";

                if (IsActiveStatus(prStatus) || IsActiveStatus(buildStatusLower) || IsActiveStatus(buildResult))
                    return TimeSpan.FromSeconds(2);

                if (IsFinalStatus(prStatus) && IsFinalStatus(buildStatusLower))
                    return _settings.FinalStatusDuration;

                return TimeSpan.FromSeconds(30);
            }

            if (value is ReleaseDto release)
            {
                if (release.Environments == null || release.Environments.Count == 0)
                    return TimeSpan.FromSeconds(5);

                bool anyActive = false;
                bool allFinal = true;

                foreach (var env in release.Environments)
                {
                    var envStatus = env.Status?.ToLower() ?? "";
                    if (IsActiveStatus(envStatus))
                    {
                        anyActive = true;
                        allFinal = false;
                        break;
                    }
                    if (!IsFinalStatus(envStatus))
                        allFinal = false;
                }

                if (anyActive)
                    return TimeSpan.FromSeconds(2);
                if (allFinal)
                    return _settings.FinalStatusDuration;
                return TimeSpan.FromSeconds(30);
            }

            return TimeSpan.FromMinutes(5);
        }

        public async Task<T> GetOrCreateAsync<T>(string cacheKey, Func<Task<T>> factory,
            CacheDuration duration = CacheDuration.Short, bool forceRefresh = false,
            string category = "default")
        {
            string fullCacheKey = string.IsNullOrEmpty(Prefix) ? cacheKey : $"{Prefix}_{cacheKey}";
            string fullCategory = string.IsNullOrEmpty(Prefix) ? category : $"{Prefix}_{category}";

            if (forceRefresh)
            {
                _memoryCache.Remove(fullCacheKey);
                _keyTracker.RemoveKey(fullCacheKey, fullCategory);
                _logger.LogDebug($"Cache forzado para: {fullCacheKey}");
            }

            // Intentar obtener de caché
            if (_memoryCache.TryGetValue(fullCacheKey, out T cachedValue))
            {
                bool shouldUseCache = true;

                if (cachedValue is BuildStatusResult buildStatus)
                {
                    var prStatus = buildStatus.PullRequestStatus?.ToLower() ?? "";
                    var buildStatusLower = buildStatus.Status?.ToLower() ?? "";

                    if (IsActiveStatus(prStatus) || IsActiveStatus(buildStatusLower))
                    {
                        shouldUseCache = false;
                        _logger.LogDebug($"Estado activo detectado en caché para {fullCacheKey}, ignorando caché");
                    }
                    else
                    {
                        _logger.LogDebug($"Cache hit (estado final) para: {fullCacheKey}");
                        return cachedValue;
                    }
                }
                else if (cachedValue is ReleaseDto release)
                {
                    bool hasActiveEnvironments = false;
                    if (release.Environments != null)
                    {
                        foreach (var env in release.Environments)
                        {
                            if (IsActiveStatus(env.Status))
                            {
                                hasActiveEnvironments = true;
                                break;
                            }
                        }
                    }

                    if (hasActiveEnvironments)
                    {
                        shouldUseCache = false;
                    }
                    else
                    {
                        _logger.LogDebug($"Cache hit (todos finales) para: {fullCacheKey}");
                        return cachedValue;
                    }
                }
                else
                {
                    _logger.LogDebug($"Cache hit para: {fullCacheKey}");
                    return cachedValue;
                }

                if (!shouldUseCache)
                {
                    _memoryCache.Remove(fullCacheKey);
                    _keyTracker.RemoveKey(fullCacheKey, fullCategory);
                }
            }

            _logger.LogDebug($"Cache miss para: {fullCacheKey}");

            T value;
            try
            {
                value = await factory();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error en factory para clave: {fullCacheKey}");
                throw;
            }

            var cacheDuration = GetCacheDurationForValue(value, duration);

            // No cachear si la duración es muy corta (estado activo)
            if (cacheDuration.TotalSeconds <= 5)
            {
                _logger.LogDebug($"No se cacheará {fullCacheKey} (estado activo, duración: {cacheDuration})");
                return value;
            }

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = cacheDuration,
                Priority = CacheItemPriority.Normal,
                Size = 1
            };

            cacheOptions.RegisterPostEvictionCallback((key, value, reason, state) =>
            {
                var keyStr = key as string;
                _logger.LogTrace($"Cache expirada para: {keyStr}, razón: {reason}");
                _keyTracker.RemoveKey(keyStr, fullCategory);
            });

            _memoryCache.Set(fullCacheKey, value, cacheOptions);
            _keyTracker.RegisterKey(fullCacheKey, fullCategory);

            _logger.LogDebug($"Clave {fullCacheKey} cacheada por {cacheDuration.TotalMinutes:F2} minutos");
            return value;
        }

        // Métodos específicos de PullRequest y Build
        public async Task<BuildStatusResult> GetOrCreatePullRequestStatusAsync(int prId, Func<Task<BuildStatusResult>> factory, bool forceRefresh = false)
        {
            var cacheKey = $"pr_status_{prId}";
            var category = $"pr_status_{prId}";
            return await GetOrCreateAsync(cacheKey, factory, CacheDuration.Short, forceRefresh, category);
        }

        public async Task<ReleaseDto> GetOrCreatePullRequestReleasesAsync(int prId, Func<Task<ReleaseDto>> factory, bool forceRefresh = false)
        {
            var cacheKey = $"pr_releases_{prId}";
            var category = $"pr_releases_{prId}";
            return await GetOrCreateAsync(cacheKey, factory, CacheDuration.Short, forceRefresh, category);
        }

        public async Task<BuildStatusResult> GetOrCreatePullRequestStatusGxAsync(int prId, string repositoryId,
            Func<Task<BuildStatusResult>> factory, bool forceRefresh = false)
        {
            var cacheKey = $"pr_status_gx_{prId}_{repositoryId}";
            var category = $"pr_status_gx_{prId}";
            return await GetOrCreateAsync(cacheKey, factory, CacheDuration.Short, forceRefresh, category);
        }

        public async Task<ReleaseDto> GetOrCreatePullRequestReleasesGxAsync(int prId,
            Func<Task<ReleaseDto>> factory, bool forceRefresh = false)
        {
            var cacheKey = $"pr_releases_gx_{prId}";
            var category = $"pr_releases_gx_{prId}";
            return await GetOrCreateAsync(cacheKey, factory, CacheDuration.Short, forceRefresh, category);
        }

        public async Task<BuildStatusResult> GetOrCreateBuildStatusAsync(int prId,
            Func<Task<BuildStatusResult>> factory, bool forceRefresh = false)
        {
            var cacheKey = $"build_status_{prId}";
            var category = $"build_{prId}";
            return await GetOrCreateAsync(cacheKey, factory, CacheDuration.Short, forceRefresh, category);
        }

        // Invalidación
        public void InvalidatePullRequestCache(int prId)
        {
            var patterns = new[]
            {
                $"pr_status_{prId}",
                $"pr_releases_{prId}",
                $"pr_status_gx_{prId}",
                $"pr_releases_gx_{prId}",
                $"build_status_{prId}",
                $"build_{prId}"
            };

            int removedCount = 0;
            foreach (var pattern in patterns)
            {
                var keys = _keyTracker.GetKeysByCategory(pattern).ToList();
                foreach (var key in keys)
                {
                    _memoryCache.Remove(key);
                    _keyTracker.RemoveKey(key, pattern);
                    removedCount++;
                }
            }

            _logger.LogInformation($"Cache invalidada para PR {prId}, {removedCount} entradas removidas");
        }

        public void InvalidateBuildCache(int prId)
        {
            InvalidatePullRequestCache(prId);
            _logger.LogInformation($"Cache invalidada específicamente para Build PR {prId}");
        }

        public void InvalidateAllCache()
        {
            var categories = _keyTracker.GetCategoryCounts().Keys.ToList();
            int totalRemoved = 0;

            foreach (var category in categories)
            {
                var keys = _keyTracker.GetKeysByCategory(category).ToList();
                foreach (var key in keys)
                {
                    _memoryCache.Remove(key);
                    _keyTracker.RemoveKey(key, category);
                    totalRemoved++;
                }
            }

            _logger.LogInformation($"Toda la caché invalidada, {totalRemoved} entradas removidas");
        }

        // Estadísticas
        public CacheStatistics GetCacheStatistics()
        {
            return new CacheStatistics
            {
                TotalEntries = _keyTracker.TotalEntries,
                TotalCategories = _keyTracker.TotalCategories,
                CategoryCounts = _keyTracker.GetCategoryCounts()
            };
        }

        // Sobrecarga para compatibilidad (sin categoría)
        public async Task<T> GetOrCreateAsync<T>(string cacheKey, Func<Task<T>> factory,
            CacheDuration duration = CacheDuration.Short, bool forceRefresh = false)
        {
            return await GetOrCreateAsync(cacheKey, factory, duration, forceRefresh, "default");
        }
    }

    public class CacheStatistics
    {
        public int TotalEntries { get; set; }
        public int TotalCategories { get; set; }
        public Dictionary<string, int> CategoryCounts { get; set; } = new Dictionary<string, int>();
    }
}