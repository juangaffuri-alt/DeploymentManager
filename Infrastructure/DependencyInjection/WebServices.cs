using Microsoft.AspNetCore.Mvc;
using PasadorScript.Presentation.Middleware;
using PasadorScript.Services.Azure;

namespace PasadorScript.Infrastructure.DependencyInjection;

public static class WebServices
{
    public static IServiceCollection AddWebServices(this IServiceCollection services)
    {
        services.AddControllersWithViews(options =>
        {
            options.Filters.Add<ReturnUrlFilter>();
        });

        services.AddHttpClient();
        services.AddScoped<AzureHttpClient>();
        services.AddScoped<AzureHttpClientUser>();
        services.AddScoped<AzureHttpClientLegacy>();
        services.AddScoped<AzureHttpClientConfigured>();

        return services;
    }
}