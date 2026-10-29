using Microsoft.Extensions.DependencyInjection;

namespace AzureReleasesSync.Worker;

/// <summary>
/// Réplica local del patrón de WorkerHostBuilder (bpn.SchedulerTasksCommon) para que este
/// repo compile aislado. En la solución del scheduler, borrá esta clase y usá directamente
/// el WorkerHostBuilder de la librería: la firma es idéntica a la que ya usás en WorkerAfip:
///   var host = await WorkerHostBuilder.CreateWorkerHostAsync(args, (context, services) => {...}, serviceName: "...");
/// </summary>
public static class WorkerHostBuilder
{
    public static Task<IHost> CreateWorkerHostAsync(
        string[] args,
        Action<HostBuilderContext, IServiceCollection> configureServices,
        string? serviceName = null)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // Comportamiento por defecto del host de workers: appsettings.json + variables de entorno.
        builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
        builder.Configuration.AddEnvironmentVariables();

        builder.Logging.AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
        });

        if (serviceName is not null)
            builder.Services.AddSingleton(new WorkerServiceName(serviceName));

        // HostApplicationBuilder no expone un HostBuilderContext; lo armamos equivalente
        // para mantener la misma callback (context, services) que en la librería real.
        var context = new HostBuilderContext(new Dictionary<object, object>())
        {
            Configuration = builder.Configuration,
            HostingEnvironment = builder.Environment,
        };

        configureServices(context, builder.Services);

        return Task.FromResult(builder.Build());
    }
}

/// <summary>Nombre del servicio (opcional), análogo al ApplicationName del ensamblado.</summary>
public sealed record WorkerServiceName(string Name);
