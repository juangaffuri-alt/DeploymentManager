using AzureReleasesSync;
using Microsoft.Extensions.DependencyInjection;

namespace AzureReleasesSync.Worker;

/// <summary>
/// HostedService residente que dispara la sincronización todos los días a una hora local
/// configurable (clave "Worker:RunAt", formato HH:mm, default 03:00). Es la alternativa a
/// <see cref="RunOnceHostedService"/> cuando este binario debe autodirigirse sin un
/// planificador externo. En Program.cs se documenta cómo conmutar entre ambos modos.
/// </summary>
public sealed class AzureReleasesDailyRunner : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AzureReleasesDailyRunner> _logger;

    public AzureReleasesDailyRunner(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<AzureReleasesDailyRunner> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var runAt = ParseRunAt(_configuration["Worker:RunAt"]);
        _logger.LogInformation("AzureReleasesDailyRunner residente. Próxima corrida diaria a las {RunAt:hh\\:mm} hora local.", runAt);

        while (!stoppingToken.IsCancellationRequested)
        {
            var next = GetNextOccurrence(runAt, DateTimeOffset.Now);
            var delay = next - DateTimeOffset.Now;
            if (delay > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break; // shutdown del host
                }
            }

            await RunOnceSafeAsync(stoppingToken);
        }

        _logger.LogInformation("AzureReleasesDailyRunner detenido.");
    }

    /// <summary>Corre la tarea en un scope nuevo; nunca deja morir al runner por un error de corrida.</summary>
    private async Task RunOnceSafeAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var task = scope.ServiceProvider.GetRequiredService<AzureReleasesSyncTask>();
            await task.Run(ct);
        }
        catch (OperationCanceledException)
        {
            // Shutdown durante una corrida: el watermark no avanzó y mañana se recupera la ventana.
            _logger.LogWarning("Corrida interrumpida por shutdown del host.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error no controlado en la corrida diaria; se reintenta mañana.");
        }
    }

    private static TimeSpan ParseRunAt(string? value) =>
        TimeSpan.TryParse(value, out var ts) && ts >= TimeSpan.Zero && ts < TimeSpan.FromDays(1)
            ? ts
            : new TimeSpan(3, 0, 0); // default 03:00

    /// <summary>Próxima ocurrencia de la hora local dada, manejando cambios de DST.</summary>
    private static DateTimeOffset GetNextOccurrence(TimeSpan timeOfDay, DateTimeOffset nowLocal)
    {
        var today = nowLocal.Date + timeOfDay;
        return today > nowLocal ? today : today.AddDays(1);
    }
}
