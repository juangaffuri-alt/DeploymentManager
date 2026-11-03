# AzureReleasesSync (consola .NET 10)

Herramienta de volcado de **releases de Azure DevOps** a la base `DeploymentManager_DF`
(misma connection string `NuevoP229` de la aplicación), para auditoría interna antes de
que Azure DevOps los elimine por retención (365 días).

## Qué replica del informe gerencial

La consulta es la misma que usa `InformeGerencialReleasesService` vía
`AzureReleaseService.ListReleasesForReportAsync`:

```
GET {vsrm}/_apis/release/releases?definitionId={def}
    &$expand=artifacts,environments,approvals
    &minCreatedTime={fecha}&$top=100&queryOrder=descending&api-version=7.1
```

con paginación por `x-ms-continuationtoken`, y el mismo mapeo de:
- artifacts → `PullRequestId`, `BuildId`, `RepositoryId`, `BuildNumber`
- environments → nombre/rank/status/queuedOn/completedOn (ultimo deploy step *aplicado*)
- approvals PRE/POST anidadas + aprobaciones raíz (`approvals`) asignadas al environment
  cuando el listado no las trae anidadas (equivalente a `ApplyRootApprovals`).

A diferencia del informe, **no se cruza con los envíos locales**: se vuelcan TODOS los
releases, incluido el JSON crudo (`RawJson`) como salvaguarda.

## Tablas destino (ver `../../sql/CreateAzureReleasesTables.sql`)

| Tabla | Contenido |
|---|---|
| `dbo.AzureRelease` | cabecera por release (clave natural `ProjectName + ReleaseId`) |
| `dbo.AzureReleaseEnvironment` | stages/environments de cada release |
| `dbo.AzureReleaseApproval` | aprobaciones PRE/POST por attempt (histórico, nunca se borra) |
| `dbo.AzureReleaseSyncLog` | bitácora de corridas del sincronizador |

## Comportamiento

- **Idempotente**: upserts (`MERGE ... WITH (HOLDLOCK)`); puede correr varias veces sin duplicar.
- **Incremental con watermark**: toma `MAX(LastSyncAtUtc)` de `dbo.AzureRelease` y pide releases
  desde ese punto **menos `OverlapDays`** (default 7) para re-refrescar releases recientes
  (los approvals cambian de estado después de creados).
- **Backfill inicial**: si la tabla está vacía, pide `BackfillDays` (default **364**, un día de
  margen sobre los 365 de retención). También forzado con `--force-backfill`.
- **Seguridad ante truncado**: si una definición alcanza el tope de páginas (`MaxPagesPerDefinition`),
  la corrida queda marcada `Success=0, Truncated=1` en `AzureReleaseSyncLog` y el exit code es 1,
  para que el scheduler/monitoreo lo detecte. El backfill siguiente recompleta.

## Uso

```bash
# 1. Ejecutar sql/CreateAzureReleasesTables.sql en DeploymentManager_DF
# 2. Copiar appsettings.example.json -> appsettings.json y completar PAT y conexiones
#    (o simplemente apuntar este proyecto al appsettings.json completo de PasadorScript:
#     las secciones ConnectionStrings / AzureSettings / Projects tienen el mismo formato)
dotnet run -c Release                                  # corrida incremental (o backfill si está vacía)
Sync__ForceBackfill=true dotnet run -c Release         # backfill forzado completo
Sync__BackfillDays=200 Sync__OverlapDays=10 dotnet run -c Release   # ventana custom
```

El Program.cs replica el patrón de WorkerAfip:

```csharp
var host = await WorkerHostBuilder.CreateWorkerHostAsync(args, (context, services) =>
{
    services.AddScoped<ITask, AzureReleasesSyncTask>();   // la unidad de trabajo
    services.AddHostedService<RunOnceHostedService>();   // corre al arrancar y termina
},
serviceName: "AzureReleasesSync");

await host.RunAsync();
```

Exit codes: `0` todo ok · `1` errores o datos truncados · `2` fallo fatal.

## Modo worker diario (ITask de Bpn.SchedulerTasksCommon)

El corazón del proyecto es `Worker/AzureReleasesSyncTask.cs` → clase `AzureReleasesSyncTask : ITask`:

- Implementa `Name` y `Run(CancellationToken)`; el scheduling lo maneja tu scheduler, no esta clase.
- `Run` nunca lanza salvo cancelación: loguea OK/errores y deja constancia en
  `dbo.AzureReleaseSyncLog`. Si una corrida falla, el watermark no avanza y la corrida
  del día siguiente recupera automáticamente la ventana perdida (solapamiento de 7 días).
- Primera corrida = backfill de 364 días; las siguientes son incrementales. Misma lógica
  núcleo (`ReleaseSyncService`) en todos los modos: cero duplicación de código.
- Backfill manual desde código/admin: `await task.RunBackfillAsync(ct)` (equivale a
  correr la app con la variable de entorno `Sync:ForceBackfill=true`).

### Los dos modos de alojamiento (se eligen en Program.cs)

| Modo | HostedService | Comportamiento |
|---|---|---|
| **MODO 1 (default)** | `RunOnceHostedService` | Corre la `ITask` una vez al arrancar y el proceso termina con exit code. Ideal para que tu scheduler / cron / Task Scheduler levante el binario a diario. |
| **MODO 2** | `AzureReleasesDailyRunner` | El binario queda residente y se autodirige todos los días a la hora de `Worker:RunAt` (default `03:00`). Para activarlo: comentá `AddHostedService<RunOnceHostedService>()` y descomentá `AddHostedService<AzureReleasesDailyRunner>()`. |

> `Worker/WorkerHostBuilder.cs` es un stub local con la misma firma que el de
> `Bpn.SchedulerTasksCommon` (`CreateWorkerHostAsync(args, (context, services) => {...}, serviceName:)`)
> para que este repo compile aislado. Al llevar el código a tu solución, borrá ese archivo
> y usá el `WorkerHostBuilder` real de la librería — tu `Program.cs` queda idéntico al de WorkerAfip.

### Pasos para integrarlo en tu solución del scheduler

1. Copiá los archivos del núcleo a un proyecto de tu solución (o referenciá este):
   `SyncOptions.cs`, `SyncRunResult.cs`, `AzureReleaseClient.cs`, `ReleaseSyncService.cs`,
   `ReleaseRepository.cs`, `Models/AzureReleaseItem.cs`, `GlobalUsings.cs` y
   `Worker/AzureReleasesSyncTask.cs`.
2. En `AzureReleasesSyncTask.cs`: cambiá el namespace por el de tu proyecto, agregá
   `using Bpn.SchedulerTasksCommon;` y **borrá el bloque `#if !TASKSLIB ... #endif`** del
   final (es sólo un stub para que este repo compile aislado; la interfaz real viene del
   paquete). Si tu `ITask` define el método con otro nombre (p. ej. `ExecuteAsync`),
   renombralo en la clase; el cuerpo no cambia.
3. Agregá el `PackageReference` `Microsoft.Data.SqlClient` al proyecto que lo reciba
   (el resto —`IConfiguration`, `ILoggerFactory`— ya lo provee el host).
4. Registrala en el lambda de `WorkerHostBuilder.CreateWorkerHostAsync`, igual que en WorkerAfip:
   ```csharp
   services.AddScoped<ITask, AzureReleasesSyncTask>();
   ```
   y programala con frecuencia diaria desde tu scheduler (recomendado: fuera del pico, p. ej. 03:00 hs).
5. Copiá las secciones `ConnectionStrings:NuevoP229`, `AzureSettings`, `Projects` y `Sync`
   al `appsettings.json` del host del scheduler (mismo formato que PasadorScript).

La consola sigue disponible para ejecuciones puntuales/backfills forzados desde línea de
comandos; ambos modos comparten exactamente la misma lógica de sincronización.

> Nota: el proyecto usa `Microsoft.Data.SqlClient` directamente (sin EF Core) para poder
> pegarlo a cualquier solución sin arrastrar el DbContext de la web.
