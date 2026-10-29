
// Al integrar esta clase en tu solución de scheduler:
//   1) cambiá el namespace de abajo por el de tu proyecto (ej.: TuApp.Tasks);
//   2) agregá la referencia al PackageReference "Bpn.SchedulerTasksCommon" y descomentá el using.
// using Bpn.SchedulerTasksCommon;

namespace AzureReleasesSync;

/// <summary>
/// Task del scheduler (bpn.SchedulerTasksCommon) que vuelca releases de Azure DevOps
/// a la base DeploymentManager_DF. Diseñada para correr diario: la primera ejecución
/// hace backfill completo y las siguientes son incrementales (watermark + solapamiento).
/// El scheduling (frecuencia diaria, reintentos, etc.) lo administra el host que usa la
/// librería; esta clase sólo implementa la unidad de trabajo.
/// </summary>
/// <remarks>
/// Contrato asumido de Bpn.SchedulerTasksCommon.ITask (ajustar si difiere):
///   public interface ITask
///   {
///       string Name { get; }
///       Task Run(CancellationToken cancellationToken);   // o ExecuteAsync(...)
///   }
/// Si el miembro se llama distinto, renombralo acá; el cuerpo no cambia.
/// </remarks>
public class AzureReleasesSyncTask : ITask
{
    private readonly IConfiguration _configuration;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<AzureReleasesSyncTask> _logger;

    public AzureReleasesSyncTask(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        _configuration = configuration;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<AzureReleasesSyncTask>();
    }

    /// <summary>Nombre con el que el scheduler registra/logea la tarea.</summary>
    public string Name => "AzureReleasesSync";

    /// <summary>
    /// Punto de entrada que invoca el scheduler. Nunca lanza salvo cancelación:
    /// cualquier excepción se loguea y se reporta como corrida fallada (el watermark
    /// no avanza y la próxima corrida diaria recupera automáticamente la ventana perdida).
    /// </summary>
    public async Task Run(CancellationToken cancellationToken)
    {
        try
        {
            await RunOnceAsync(forceFullBackfill: false, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[{Task}] Cancelada por shutdown del host.", Name);
            throw; // el scheduler debe saber que no fue una corrida completa
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Task}] Error no controlado durante la sincronización.", Name);
        }
    }

    /// <summary>
    /// Backfill manual/forzado desde código o desde un endpoint de administración,
    /// equivalente a ejecutar la consola con --force-backfill.
    /// </summary>
    public async Task<SyncRunResult> RunBackfillAsync(CancellationToken cancellationToken)
    {
        _logger.LogWarning("[{Task}] Backfill forzado solicitado.", Name);
        return await RunOnceAsync(forceFullBackfill: true, cancellationToken);
    }

    /// <summary>
    /// Núcleo compartido con la consola: construye opciones desde appsettings.json del host,
    /// arma el pipeline cliente Azure -> ReleaseSyncService -> ReleaseRepository y corre una
    /// sincronización (backfill en la primera corrida, incremental después).
    /// </summary>
    private async Task<SyncRunResult> RunOnceAsync(bool forceFullBackfill, CancellationToken ct)
    {
        var options = SyncOptions.Load(_configuration);

        _logger.LogInformation(
            "[{Task}] Iniciando sincronización. Definiciones: {Count} | Backfill inicial: {Backfill} días | Overlap: {Overlap} días",
            Name, options.Definitions.Count, options.BackfillDays, options.OverlapDays);

        var repository = new ReleaseRepository(options.ConnectionString);
        // Cliente nuevo por corrida (mismo ciclo de vida que en la consola): sin estado
        // compartido entre ejecuciones diarias ni riesgo de conexión muerta por inactividad.
        using var client = AzureReleaseClient.Create(options);
        var service = new ReleaseSyncService(
            options,
            client,
            repository,
            _loggerFactory.CreateLogger<ReleaseSyncService>());

        var result = await service.RunAsync(forceFullBackfill, ct);

        if (result.Success)
        {
            _logger.LogInformation(
                "[{Task}] OK: {Upserted} releases, {Ok}/{Total} definiciones.",
                Name, result.ReleasesUpserted, result.DefinitionsOk, result.DefinitionsTotal);
        }
        else
        {
            _logger.LogError(
                "[{Task}] FINALIZÓ CON PROBLEMAS: {Errors} errores, truncado={Truncated}, {Ok}/{Total} definiciones ok. " +
                "El watermark NO avanzó: la próxima corrida recupera la ventana.",
                Name, result.Errors, result.Truncated, result.DefinitionsOk, result.DefinitionsTotal);
        }

        return result;
    }
}

#if !TASKSLIB
/// <summary>
/// Stub local del contrato de Bpn.SchedulerTasksCommon.ITask para que este proyecto
/// compile de forma aislada en este repo. En la solución del scheduler NO se compila
/// (definí la constant 'TASKSLIB' o borrá este bloque): la interfaz real proviene del
/// paquete Bpn.SchedulerTasksCommon. Si los miembros difieren (p. ej. ExecuteAsync en
/// lugar de Run), ajustá tanto este stub como la implementación de arriba.
/// </summary>
public interface ITask
{
    string Name { get; }

    Task Run(CancellationToken cancellationToken);
}
#endif
