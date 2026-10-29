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

Para integrarla como background diario en su solución existente: ejecutar el `.exe`/`dotnet run`
una vez por día (la ventana incremental solapa 7 días, por lo que tolera corridas falladas
intermedias dentro de ese margen — revisar `AzureReleaseSyncLog` si hay `Success=0`).

> Nota: el proyecto usa `Microsoft.Data.SqlClient` directamente (sin EF Core) para poder
> pegarlo a cualquier solución sin arrastrar el DbContext de la web.
