using Microsoft.EntityFrameworkCore;
using PasadorScript.DbModels;
using PasadorScript.Services.AdGroups.Seeds;

namespace PasadorScript.Infrastructure.DependencyInjection;

public static class InfrastructureServices
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database Context
        services.AddDbContext<NuevoP229Context>(options =>
            options.UseLazyLoadingProxies(true)
                .UseSqlServer(configuration.GetConnectionString("NuevoP229")));

        // Caching
        services.SeedTablasParametricas();
        services.AddMemoryCache();

        return services;
    }
}