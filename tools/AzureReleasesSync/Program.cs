using AzureReleasesSync;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

// Argumentos reconocidos: --force-backfill  |  --backfill-days=N  |  --overlap-days=N
var forceFullBackfill = args.Contains("--force-backfill", StringComparer.OrdinalIgnoreCase);
if (TryGetOptionValue(args, "--backfill-days", out var backfillDays))
    builder.Configuration["Sync:BackfillDays"] = backfillDays.ToString();
if (TryGetOptionValue(args, "--overlap-days", out var overlapDays))
    builder.Configuration["Sync:OverlapDays"] = overlapDays.ToString();

builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
});

var options = SyncOptions.Load(builder.Configuration);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

using var client = new AzureReleaseClient(options);
var repository = new ReleaseRepository(options.ConnectionString);
var loggerFactory = LoggerFactory.Create(logging => logging.AddSimpleConsole(o => o.SingleLine = true));
var logger = loggerFactory.CreateLogger("AzureReleasesSync");

logger.LogInformation(
    "AzureReleasesSync iniciado. Definiciones: {Count} | Backfill inicial: {Backfill} días | Overlap incremental: {Overlap} días",
    options.Definitions.Count, options.BackfillDays, options.OverlapDays);

var service = new ReleaseSyncService(options, client, repository, logger);

try
{
    var exitCode = await service.RunAsync(forceFullBackfill, cts.Token);
    logger.LogInformation("Exit code: {ExitCode} (0 = todo ok)", exitCode);
    return exitCode;
}
catch (OperationCanceledException)
{
    logger.LogWarning("Sincronización cancelada por el usuario.");
    return 130;
}
catch (Exception ex)
{
    logger.LogError(ex, "Fallo fatal en la sincronización de releases.");
    return 2;
}

static bool TryGetOptionValue(string[] args, string name, out int value)
{
    value = 0;
    var arg = args.FirstOrDefault(a => a.StartsWith($"{name}=", StringComparison.OrdinalIgnoreCase));
    if (arg is not null)
        return int.TryParse(arg[(name.Length + 1)..], out value);

    var idx = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
    if (idx >= 0 && idx + 1 < args.Length)
        return int.TryParse(args[idx + 1], out value);

    return false;
}
