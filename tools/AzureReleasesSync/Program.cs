using AzureReleasesSync;
using AzureReleasesSync.Worker;
using Microsoft.Extensions.DependencyInjection;

var host = await WorkerHostBuilder.CreateWorkerHostAsync(args, (context, services) =>
{
    // Núcleo de sincronización: un solo registro ITask para toda la app.
    // Scoped porque ReleaseRepository no es thread-safe y cada corrida debe ser aislada.
    services.AddScoped<ITask, AzureReleasesSyncTask>();

    // MODO 1 (default): corrida única al arrancar el host y el proceso termina.
    // Ideal para que un planificador externo (cron, systemd timer, Task Scheduler,
    // o tu scheduler de Bpn.SchedulerTasksCommon) levante el binario a diario.
    services.AddHostedService<RunOnceHostedService>();

    // MODO 2 (opcional): este mismo binario queda residente y se autodirige todos los
    // días a la hora configurada en "Worker:RunAt" (default 03:00). Para activarlo,
    // comentá la línea de arriba y descomentá esta:
    //services.AddHostedService<AzureReleasesDailyRunner>();
},
serviceName: "AzureReleasesSync");

await host.RunAsync();
Console.WriteLine("Host finalizado");

// Código de salida: 0 = última corrida exitosa, 1 = terminó con errores parciales,
// 2 = nunca llegó a correr (config inválida / fallo fatal). En MODO 2 siempre 0
// (el runner reporta por log; no aplica exit code por corrida).
return host.Services.GetService<RunOnceHostedService>()?.LastResult switch
{
    { } r when r.Success => 0,
    { } => 1,
    null => 2,
};
