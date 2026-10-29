using Castle.Core.Smtp;
using Mapster;
using MapsterMapper;
using PasadorScript.DTO.AzureDev;
using PasadorScript.Helpers;
using PasadorScript.Models;
using PasadorScript.Repository;
using PasadorScript.Services;
using PasadorScript.Services.AdGroups;
using PasadorScript.Services.Azure;
using PasadorScript.Services.Cache;
using PasadorScript.Services.Crypto;
using PasadorScript.Services.Email;
using PasadorScript.Services.Environments;
using PasadorScript.Services.Git;
using PasadorScript.Services.Graph;
using PasadorScript.Services.Informes;
using PasadorScript.Services.MenuApp;
using PasadorScript.Services.Pat;
using PasadorScript.Services.PolicyConfiguration;
using PasadorScript.Services.Release;
using PasadorScript.Services.RoleMapper;
using PasadorScript.Services.TeamDev;
using PasadorScript.Services.Token;
using PasadorScript.Services.TurnosFechas;
using PasadorScript.Services.User;
using QuestPDF.Infrastructure;

namespace PasadorScript.Infrastructure.DependencyInjection;

public static class ApplicationServices
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // AutoMapper
        var config = TypeAdapterConfig.GlobalSettings;
        config.Scan(typeof(Program).Assembly);

        services.AddSingleton(config);
        services.AddScoped<IMapper, ServiceMapper>();

        //services.AddSingleton(mapperConfiguration.CreateMapper());

        // Core Services
        services.AddScoped<IAdGroupService, AdGroupServiceDelegate>();
        //services.AddScoped<IRoleMapperService, RoleMapperService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IServiciosGraph, ServiciosGraph>();
        services.AddScoped<IServiciosMenu, ServiciosMenu>();
        services.AddScoped<ITokenRefresher, ServiciosTokenRefresh>();
        services.AddScoped<IDbRepository, DbRepository>();
        services.AddScoped<IPatService, PatService>();
        services.AddScoped<IPatRepositoryService, PatRepositoryService>();
        services.AddScoped<IServiciosTeamDev, ServiciosTeamDev>();
        services.AddScoped<IUserContext, UserContext>();
        services.AddScoped<IGitService, GitService>();
        services.AddScoped<IEncriptacionHelper, EncriptacionHelper>();
        services.AddScoped<IEnvironmentService, EnvironmentService>();
        services.AddScoped<IInformeGerencialReleasesService, InformeGerencialReleasesService>();
        services.AddScoped<IResolucionAprobacionRelease, ResolucionAprobacionRelease>();

        // Azure Services
        services.AddScoped<IAzureAnalyticService, AzureAnalyticService>();
        services.AddScoped<IAzureBranchService, AzureBranchService>();
        services.AddScoped<IAzureBuildService, AzureBuildService>();
        services.AddScoped<IAzureCommitService, AzureCommitService>();
        services.AddScoped<IAzureFileService, AzureFileService>();
        services.AddScoped<IAzurePatService, AzurePatService>();
        services.AddScoped<IAzurePullRequestService, AzurePullRequestService>();
        services.AddScoped<IAzureReleaseService, AzureReleaseService>();
        services.AddScoped<IAzureRepositoryService, AzureRepositoryService>();
        services.AddScoped<IAzureService, AzureService>();

        // Event Handling
        services.AddSingleton<IEventPublisher, EventPublisherPR>();
        services.AddScoped<IEventObserver<PullRequestUpdated>, DbRepository>();

        // Helpers
        services.AddScoped<TempDataHelper>();
        services.AddHttpContextAccessor();

        // Devops Cache
        // Configurar CacheSettings desde appsettings.json
        services.Configure<CacheSettings>(configuration.GetSection("CacheSettings"));

        // Configurar MemoryCache con límite de tamaño
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = configuration.GetValue<int>("CacheSettings:DevOps:MaxCacheSize") * 1024 * 1024; // Convertir MB a bytes
        });
        // Registrar el tracker de claves como singleton
        services.AddSingleton<ICacheKeyTracker, CacheKeyTracker>();
        services.AddScoped<IDevOpsCacheService, DevOpsCacheService>();
        services.AddScoped<IEmailService, EmailService>();

        // Turnos Fecha BT
        services.AddScoped<ITurnoService, TurnoService>();
        services.AddHostedService<ControlTurnosBackgroundService>();
        services.AddScoped<IFechaEntornoService, FechaEntornoService>();

        //PDF report
        QuestPDF.Settings.License = LicenseType.Community;

        return services;
    }
}