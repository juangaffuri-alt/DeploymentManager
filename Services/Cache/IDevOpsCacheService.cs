// Services/Cache/DevOpsCacheService.cs
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using PasadorScript.DTO.AzureDev;
using PasadorScript.Helpers;
using System;
using System.Threading.Tasks;

namespace PasadorScript.Services.Cache
{
    public interface IDevOpsCacheService
    {
        // Para PRs de scripts (SQL)
        Task<BuildStatusResult> GetOrCreatePullRequestStatusAsync(int prId, Func<Task<BuildStatusResult>> factory, bool forceRefresh = false);
        Task<ReleaseDto> GetOrCreatePullRequestReleasesAsync(int prId, Func<Task<ReleaseDto>> factory, bool forceRefresh = false);

        // Para PRs de Gx (builds)
        Task<BuildStatusResult> GetOrCreatePullRequestStatusGxAsync(int prId, string repositoryId, Func<Task<BuildStatusResult>> factory, bool forceRefresh = false);
        Task<ReleaseDto> GetOrCreatePullRequestReleasesGxAsync(int prId, Func<Task<ReleaseDto>> factory, bool forceRefresh = false);

        // Para builds en general (si se necesita)
        Task<BuildStatusResult> GetOrCreateBuildStatusAsync(int prId, Func<Task<BuildStatusResult>> factory, bool forceRefresh = false);

        // Invalidación
        void InvalidatePullRequestCache(int prId);
        void InvalidateBuildCache(int prId);
        void InvalidateAllCache();

        // Opcional: estadísticas
        CacheStatistics GetCacheStatistics();
    }

    public enum CacheDuration
    {
        VeryShort = 1,    // 30 segundos para estados activos
        Short = 2,        // 1 minuto
        Medium = 3,       // 5 minutos
        Long = 4,         // 30 minutos
        VeryLong = 5,     // 1 hora para estados finales
        Permanent = 6     // 4 horas (solo para datos que no cambian)
    }
}
