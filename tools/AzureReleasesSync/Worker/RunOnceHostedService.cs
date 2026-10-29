using AzureReleasesSync;
using Microsoft.Extensions.DependencyInjection;

namespace AzureReleasesSync.Worker;

/// <summary>
/// HostedService que corre UNA sola vez la tarea al arrancar el host y luego detiene
/// el application lifetime. Es el equivalente a la antigua consola "run-once":
/// ideal para que un planificador externo (cron, systemd timer, Task Scheduler)
/// levante el binario a diario.
/// Si preferís que este mismo proceso se autodirija (quede residente y dispare cada
/// día), cambiá el registro en Program.cs por <see cref="AzureReleasesDailyRunner"/>.
/// </summary>
public sealed class RunOnceHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<RunOnceHostedService> _logger;

    public RunOnceHostedService(
        IServiceScopeFactory scopeFactory,
        IHostApplicationLifetime lifetime,
        ILogger<RunOnceHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _lifetime = lifetime;
        _logger = logger;
    }

    /// <summary>Resultado de la última corrida (null si nunca llegó a ejecutarse).</summary>
    public SyncRunResult? LastResult { get; private set; }

    /// <summary>Señal interna: la corrida terminó (éxito o fallo controlado).</summary>
    private readonly TaskCompletionSource _runCompleted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Corremos en un scope propio para resolver la ITask Scoped sin bloquear el startup
        // del host (si no, el host esperaría la sincronización completa antes de "arrancar").
        _ = ExecuteAsync(cancellationToken);
        return Task.CompletedTask;
    }

    private async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var tasks = scope.ServiceProvider.GetServices<ITask>().ToList();

            if (tasks.Count == 0)
                throw new InvalidOperationException(
                    "No hay ninguna ITask registrada. Revisá el registro de servicios en Program.cs.");

            // Esta app registra exactamente una tarea; si en el futuro se agregan más,
            // se corren todas en secuencia dentro de la misma corrida del host.
            foreach (var task in tasks)
            {
                ct.ThrowIfCancellationRequested();
                _logger.LogInformation("Ejecutando tarea '{TaskName}'.", task.Name);

                await task.Run(ct);

                // El resultado granular lo expone la propia tarea vía LastResult para
                // calcular el exit code del proceso.
                if (task is AzureReleasesSyncTask syncTask)
                    LastResult = syncTask.LastResult;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Corrida cancelada por shutdown del host.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo fatal durante la corrida única.");
        }
        finally
        {
            _runCompleted.TrySetResult();
            // StopApplication hace que RunAsync() retorne y el proceso finalice con el
            // exit code definido en Program.cs.
            _lifetime.StopApplication();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // Esperamos a que termine la corrida en curso antes de apagar (graceful shutdown),
        // con un tope: si el SIGTERM trae prisa, cortamos y dejamos que la próxima corrida
        // recupere la ventana gracias al watermark.
        var delay = Task.Delay(Timeout.Infinite, cancellationToken);
        var completion = await Task.WhenAny(_runCompleted.Task, delay);

        if (completion != _runCompleted.Task)
            _logger.LogWarning("Shutdown solicitado: la corrida en curso se canceló por timeout.");
    }
}
