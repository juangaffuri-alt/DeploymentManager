using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using PasadorScript.Services.AdGroups;
using PasadorScript.Services.PolicyConfiguration;

namespace PasadorScript.Services.PolicyConfiguration
{
    public class DatabaseAuthorizationPolicyProvider : IAuthorizationPolicyProvider
    {
        private readonly DefaultAuthorizationPolicyProvider _fallbackPolicyProvider;
        private readonly IServiceProvider _serviceProvider;
        private readonly IMemoryCache _cache;
        private readonly ILogger<DatabaseAuthorizationPolicyProvider> _logger;

        public DatabaseAuthorizationPolicyProvider(
            ILogger<DatabaseAuthorizationPolicyProvider> logger,
            IOptions<AuthorizationOptions> options,
            IServiceProvider serviceProvider,
            IMemoryCache cache)
        {
            _fallbackPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
            _serviceProvider = serviceProvider;
            _cache = cache;
            _logger = logger;
        }

        public Task<AuthorizationPolicy> GetDefaultPolicyAsync() =>
            _fallbackPolicyProvider.GetDefaultPolicyAsync();

        public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() =>
            _fallbackPolicyProvider.GetFallbackPolicyAsync();

        public async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        {
            // Primero intentar con las políticas por defecto
            var policy = await _fallbackPolicyProvider.GetPolicyAsync(policyName);
            if (policy != null)
                return policy;

            // Verificar si el policyName contiene múltiples grupos separados por coma
            if (policyName.Contains(','))
            {
                return await GetMultiGroupPolicyFromDatabaseAsync(policyName);
            }

            // Si es un solo grupo, buscar en la base de datos
            return await GetSingleGroupPolicyFromDatabaseAsync(policyName);
        }

        private async Task<AuthorizationPolicy?> GetSingleGroupPolicyFromDatabaseAsync(string policyName)
        {
            // Usar caché para evitar consultas constantes
            var cacheKey = $"policy_{policyName}";

            if (_cache.TryGetValue(cacheKey, out AuthorizationPolicy? cachedPolicy))
                return cachedPolicy;

            using var scope = _serviceProvider.CreateScope();
            var adGroupService = scope.ServiceProvider.GetRequiredService<IAdGroupService>();

            try
            {
                var group = await adGroupService.GetGroupByNameAsync(policyName.Trim());
                if (group != null && group.IsActive)
                {
                    var newPolicy = new AuthorizationPolicyBuilder()
                        .AddRequirements(new MultipleAdGroupsRequirement(new[] { group.GroupName }))
                        .Build();

                    var options = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                        Size = 1
                    };

                    _cache.Set(cacheKey, newPolicy, options);
                    return newPolicy;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting policy {PolicyName}", policyName);
            }

            return null;
        }

        private async Task<AuthorizationPolicy?> GetMultiGroupPolicyFromDatabaseAsync(string policyName)
        {
            var cacheKey = $"policy_multi_{policyName.Replace(" ", "")}";

            if (_cache.TryGetValue(cacheKey, out AuthorizationPolicy? cachedPolicy))
                return cachedPolicy;

            // Separar los nombres de los grupos (manejar espacios)
            var groupNames = policyName.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                      .Select(g => g.Trim())
                                      .ToList();

            using var scope = _serviceProvider.CreateScope();
            var adGroupService = scope.ServiceProvider.GetRequiredService<IAdGroupService>();

            try
            {
                var validGroups = new List<string>();

                // Verificar que al menos uno de los grupos exista y esté activo
                foreach (var groupName in groupNames)
                {
                    var group = await adGroupService.GetGroupByNameAsync(groupName);
                    if (group != null && group.IsActive)
                    {
                        validGroups.Add(group.GroupName);
                    }
                }

                if (validGroups.Any())
                {
                    var newPolicy = new AuthorizationPolicyBuilder()
                        .AddRequirements(new MultipleAdGroupsRequirement(validGroups))
                        .Build();

                    var options = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                        Size = 1
                    };

                    _cache.Set(cacheKey, newPolicy, options);
                    return newPolicy;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting multi-group policy {PolicyName}", policyName);
            }

            return null;
        }
    }
}