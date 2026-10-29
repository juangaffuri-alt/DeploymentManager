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
dotnet run -c Release                    # corrida incremental (o backfill si está vacía)
dotnet run -c Release -- --force-backfill
dotnet run -c Release -- --backfill-days=200 --overlap-days=10
```

Exit codes: `0` todo ok · `1` errores o datos truncados · `2` fallo fatal · `130` cancelado.

## Modo worker diario (ITask de Bpn.SchedulerTasksCommon)

Existe además una unidad de trabajo lista para enchufar al scheduler:
`Worker/AzureReleasesSyncTask.cs` → clase `AzureReleasesSyncTask : ITask`.

- Implementa `Name` y `Run(CancellationToken)`; el host del scheduler la invoca **diariamente**
  (igual que WorkerAfip). No hace falta programar ningún cron propio.
- `Run` nunca lanza salvo cancelación: loguea OK/errores y deja constancia en
  `dbo.AzureReleaseSyncLog`. Si una corrida falla, el watermark no avanza y la corrida
  del día siguiente recupera automáticamente la ventana perdida (solapamiento de 7 días).
- Primera corrida = backfill de 364 días; las siguientes son incrementales. Misma lógica
  y mismo núcleo (`ReleaseSyncService`) que la consola: cero duplicación de código.
- Backfill manual desde código/admin: `await task.RunBackfillAsync(ct)` (equivale a
  `--force-backfill` de la consola).

### Pasos para integrarlo en tu solución del scheduler

1. Copiá los archivos del núcleo a un proyecto de tu solución (o referenciá este):
   `SyncOptions.cs`, `SyncRunResult.cs`, `AzureReleaseClient.cs`, `ReleaseSyncService.cs`,
   `ReleaseRepository.cs`, `Models/AzureReleaseItem.cs`, `GlobalUsings.cs` y
   `Worker/AzureReleasesSyncTask.cs`.
2. En `AzureReleasesSyncTask.cs`: cambiá el namespace por el de tu proyecto, descomentá
   `using Bpn.SchedulerTasksCommon;` y **borrá el bloque `#if !TASKSLIB ... #endif`** del
   final (es sólo un stub para que este repo compile aislado; la interfaz real viene del
   paquete). Si tu `ITask` define el método con otro nombre (p. ej. `ExecuteAsync`),
   renombralo en la clase; el cuerpo no cambia.
3. Agregá el `PackageReference` `Microsoft.Data.SqlClient` al proyecto que lo reciba
   (el resto —`IConfiguration`, `ILoggerFactory`— ya lo provee el host).
4. Registrala donde tu scheduler registre sus tasks (patrón WorkerAfip), p. ej.:
   ```csharp
   services.AddSingleton<AzureReleasesSyncTask>();
   // y en la configuración/lista de tasks del scheduler: typeof(AzureReleasesSyncTask)
   ```
5. Copiá las secciones `ConnectionStrings:NuevoP229`, `AzureSettings`, `Projects` y `Sync`
   al `appsettings.json` del host del scheduler (mismo formato que PasadorScript).
6. Programala con frecuencia diaria (recomendado: fuera del pico, p. ej. 03:00 hs).

La consola sigue disponible para ejecuciones puntuales/backfills forzados desde línea de
comandos; ambos modos comparten exactamente la misma lógica de sincronización.

> Nota: el proyecto usa `Microsoft.Data.SqlClient` directamente (sin EF Core) para poder
> pegarlo a cualquier solución sin arrastrar el DbContext de la web.
