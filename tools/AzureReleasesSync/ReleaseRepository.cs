using System.Data;
using System.Text;
using AzureReleasesSync.Models;
using Microsoft.Data.SqlClient;

namespace AzureReleasesSync;

/// <summary>
/// Persistencia idempotente (MERGE) de releases, environments y approvals en DeploymentManager_DF.
/// Re-sincronizar un release actualiza sus filas pero NUNCA elimina aprobaciones históricas.
/// </summary>
public sealed class ReleaseRepository
{
    private const int SqlServerMaxParameterCount = 2100;
    private readonly string _connectionString;

    public ReleaseRepository(string connectionString) => _connectionString = connectionString;

    /// <summary>
    /// Watermark incremental: última corrida EXITOSA de la bitácora. Si la anterior tuvo
    /// errores o quedó truncada, se retrocede hasta la última exitosa para no perder releases.
    /// </summary>
    public async Task<DateTime?> GetLastSuccessfulSyncWatermarkUtcAsync(CancellationToken ct)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(
            """
            SELECT MAX(FinishedAtUtc)
              FROM dbo.AzureReleaseSyncLog WITH (NOLOCK)
             WHERE Success = 1;
            """, conn)
        {
            CommandType = CommandType.Text,
        };

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is DateTime dt && dt != default ? dt : null;
    }

    /// <summary>Inserta una corrida de sincronización y devuelve su id.</summary>
    public async Task<long> BeginRunAsync(DateTime startedAtUtc, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(
            """
            INSERT INTO dbo.AzureReleaseSyncLog (StartedAtUtc)
            OUTPUT INSERTED.Id
            VALUES (@startedAtUtc);
            """, conn);
        cmd.Parameters.Add("@startedAtUtc", SqlDbType.DateTime).Value = startedAtUtc;

        var id = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(id);
    }

    public async Task FinishRunAsync(
        long runId,
        DateTime finishedAtUtc,
        DateTime minCreatedUtc,
        DateTime maxCreatedUtc,
        int definitionsTotal,
        int definitionsOk,
        int releasesUpserted,
        int errors,
        bool truncated,
        bool success,
        string? message,
        CancellationToken ct)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(
            """
            UPDATE dbo.AzureReleaseSyncLog
               SET FinishedAtUtc    = @finishedAtUtc,
                   MinCreatedUtc    = @minCreatedUtc,
                   MaxCreatedUtc    = @maxCreatedUtc,
                   DefinitionsTotal = @definitionsTotal,
                   DefinitionsOk    = @definitionsOk,
                   ReleasesUpserted = @releasesUpserted,
                   Errors           = @errors,
                   Truncated        = @truncated,
                   Success          = @success,
                   Message          = @message
             WHERE Id = @runId;
            """, conn);

        cmd.Parameters.Add("@finishedAtUtc", SqlDbType.DateTime).Value = finishedAtUtc;
        cmd.Parameters.Add("@minCreatedUtc", SqlDbType.DateTime).Value = minCreatedUtc;
        cmd.Parameters.Add("@maxCreatedUtc", SqlDbType.DateTime).Value = maxCreatedUtc;
        cmd.Parameters.Add("@definitionsTotal", SqlDbType.Int).Value = definitionsTotal;
        cmd.Parameters.Add("@definitionsOk", SqlDbType.Int).Value = definitionsOk;
        cmd.Parameters.Add("@releasesUpserted", SqlDbType.Int).Value = releasesUpserted;
        cmd.Parameters.Add("@errors", SqlDbType.Int).Value = errors;
        cmd.Parameters.Add("@truncated", SqlDbType.Bit).Value = truncated;
        cmd.Parameters.Add("@success", SqlDbType.Bit).Value = success;
        cmd.Parameters.Add("@message", SqlDbType.NVarChar, -1).Value = (object?)message ?? DBNull.Value;
        cmd.Parameters.Add("@runId", SqlDbType.BigInt).Value = runId;

        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Upsert completo de un release (cabecera + environments + approvals) en una sola transacción.
    /// Devuelve el id local (dbo.AzureRelease.Id).
    /// </summary>
    public async Task<long> UpsertReleaseAsync(AzureReleaseItem release, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(ct);

        try
        {
            var releaseIdLocal = await UpsertReleaseHeaderAsync(conn, tx, release, ct);

            foreach (var env in release.Environments)
            {
                var envIdLocal = await UpsertEnvironmentAsync(conn, tx, releaseIdLocal, env, ct);

                await UpsertApprovalsAsync(conn, tx, envIdLocal, "PRE", env.PreDeployApprovals, ct);
                await UpsertApprovalsAsync(conn, tx, envIdLocal, "POST", env.PostDeployApprovals, ct);
            }

            await tx.CommitAsync(ct);
            return releaseIdLocal;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private static async Task<long> UpsertReleaseHeaderAsync(
        SqlConnection conn, SqlTransaction tx, AzureReleaseItem release, CancellationToken ct)
    {
        const string sql =
            """
            MERGE dbo.AzureRelease WITH (HOLDLOCK) AS t
            USING (SELECT @ProjectName AS ProjectName, @ReleaseId AS ReleaseId) AS s
                ON t.ProjectName = s.ProjectName AND t.ReleaseId = s.ReleaseId
            WHEN MATCHED THEN UPDATE SET
                ReleaseDefinitionId = @ReleaseDefinitionId,
                ReleaseName         = @ReleaseName,
                Status              = @Status,
                CreatedOnUtc        = @CreatedOnUtc,
                ModifiedOnUtc       = @ModifiedOnUtc,
                PullRequestId       = COALESCE(@PullRequestId, t.PullRequestId),
                BuildId             = COALESCE(@BuildId, t.BuildId),
                RepositoryId        = COALESCE(@RepositoryId, t.RepositoryId),
                BuildNumber         = COALESCE(@BuildNumber, t.BuildNumber),
                ReleaseUrl          = COALESCE(@ReleaseUrl, t.ReleaseUrl),
                Description         = COALESCE(@Description, t.Description),
                RawJson             = @RawJson,
                LastSyncAtUtc       = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT (
                ProjectName, ReleaseDefinitionId, ReleaseId, ReleaseName, Status,
                CreatedOnUtc, ModifiedOnUtc, PullRequestId, BuildId, RepositoryId,
                BuildNumber, ReleaseUrl, Description, RawJson, FirstSyncAtUtc, LastSyncAtUtc)
            VALUES (
                @ProjectName, @ReleaseDefinitionId, @ReleaseId, @ReleaseName, @Status,
                @CreatedOnUtc, @ModifiedOnUtc, @PullRequestId, @BuildId, @RepositoryId,
                @BuildNumber, @ReleaseUrl, @Description, @RawJson, SYSUTCDATETIME(), SYSUTCDATETIME())
            OUTPUT INSERTED.Id;
            """;

        await using var cmd = new SqlCommand(sql, conn, tx) { CommandType = CommandType.Text };
        AddReleaseParameters(cmd, release);

        var id = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(id);
    }

    private static void AddReleaseParameters(SqlCommand cmd, AzureReleaseItem release)
    {
        cmd.Parameters.Add("@ProjectName", SqlDbType.NVarChar, 255).Value = release.ProjectName;
        cmd.Parameters.Add("@ReleaseDefinitionId", SqlDbType.Int).Value = release.ReleaseDefinitionId;
        cmd.Parameters.Add("@ReleaseId", SqlDbType.Int).Value = release.ReleaseId;
        cmd.Parameters.Add("@ReleaseName", SqlDbType.NVarChar, 500).Value = DbVal(release.Name);
        cmd.Parameters.Add("@Status", SqlDbType.NVarChar, 50).Value = DbVal(release.Status);
        cmd.Parameters.Add("@CreatedOnUtc", SqlDbType.DateTime).Value = release.CreatedOnUtc == default ? DateTime.MinValue : release.CreatedOnUtc;
        cmd.Parameters.Add("@ModifiedOnUtc", SqlDbType.DateTime).Value = DbVal(release.ModifiedOnUtc);
        cmd.Parameters.Add("@PullRequestId", SqlDbType.Int).Value = DbVal(release.PullRequestId);
        cmd.Parameters.Add("@BuildId", SqlDbType.Int).Value = DbVal(release.BuildId);
        cmd.Parameters.Add("@RepositoryId", SqlDbType.UniqueIdentifier).Value = DbVal(release.RepositoryId);
        cmd.Parameters.Add("@BuildNumber", SqlDbType.NVarChar, 255).Value = DbVal(release.BuildNumber);
        cmd.Parameters.Add("@ReleaseUrl", SqlDbType.NVarChar, 1000).Value = DbVal(release.ReleaseUrl);
        cmd.Parameters.Add("@Description", SqlDbType.NVarChar, -1).Value = DbVal(release.Description);
        cmd.Parameters.Add("@RawJson", SqlDbType.NVarChar, -1).Value = release.RawJson;
    }

    private static async Task<long> UpsertEnvironmentAsync(
        SqlConnection conn, SqlTransaction tx, long azureReleaseId, AzureEnvironmentItem env, CancellationToken ct)
    {
        const string sql =
            """
            MERGE dbo.AzureReleaseEnvironment WITH (HOLDLOCK) AS t
            USING (SELECT @AzureReleaseId AS AzureReleaseId, @Name AS Name, @Rank AS Rank) AS s
                ON t.AzureReleaseId = s.AzureReleaseId AND t.Name = s.Name AND t.Rank = s.Rank
            WHEN MATCHED THEN UPDATE SET
                EnvironmentId         = @EnvironmentId,
                Status                = @Status,
                Attempt               = @Attempt,
                QueuedOnUtc           = @QueuedOnUtc,
                StartedOnUtc          = @StartedOnUtc,
                CompletedOnUtc        = @CompletedOnUtc,
                DeployedByDisplayName = @DeployedByDisplayName,
                RawJson               = @RawJson,
                LastSyncAtUtc         = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT (
                AzureReleaseId, EnvironmentId, Name, Rank, Status, Attempt,
                QueuedOnUtc, StartedOnUtc, CompletedOnUtc, DeployedByDisplayName,
                RawJson, FirstSyncAtUtc, LastSyncAtUtc)
            VALUES (
                @AzureReleaseId, @EnvironmentId, @Name, @Rank, @Status, @Attempt,
                @QueuedOnUtc, @StartedOnUtc, @CompletedOnUtc, @DeployedByDisplayName,
                @RawJson, SYSUTCDATETIME(), SYSUTCDATETIME())
            OUTPUT INSERTED.Id;
            """;

        await using var cmd = new SqlCommand(sql, conn, tx);
        cmd.Parameters.Add("@AzureReleaseId", SqlDbType.BigInt).Value = azureReleaseId;
        cmd.Parameters.Add("@EnvironmentId", SqlDbType.Int).Value = env.EnvironmentId;
        cmd.Parameters.Add("@Name", SqlDbType.NVarChar, 255).Value = env.Name;
        cmd.Parameters.Add("@Rank", SqlDbType.Int).Value = env.Rank;
        cmd.Parameters.Add("@Status", SqlDbType.NVarChar, 50).Value = DbVal(NullIfEmpty(env.Status));
        cmd.Parameters.Add("@Attempt", SqlDbType.Int).Value = DbVal(env.Attempt == 0 ? null : env.Attempt);
        cmd.Parameters.Add("@QueuedOnUtc", SqlDbType.DateTime).Value = DbVal(env.QueuedOnUtc);
        cmd.Parameters.Add("@StartedOnUtc", SqlDbType.DateTime).Value = DbVal(env.StartedOnUtc);
        cmd.Parameters.Add("@CompletedOnUtc", SqlDbType.DateTime).Value = DbVal(env.CompletedOnUtc);
        cmd.Parameters.Add("@DeployedByDisplayName", SqlDbType.NVarChar, 255).Value = DbVal(env.DeployedByDisplayName);
        cmd.Parameters.Add("@RawJson", SqlDbType.NVarChar, -1).Value = env.RawJson;

        var id = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(id);
    }

    private static async Task UpsertApprovalsAsync(
        SqlConnection conn, SqlTransaction tx, long envId, string approvalType,
        IReadOnlyList<AzureApprovalItem> approvals, CancellationToken ct)
    {
        if (approvals.Count == 0)
            return;

        // Chunk para no superar el límite de parámetros de SQL Server (2100).
        foreach (var chunk in ApprovalsChunks(approvals))
        {
            var sql = BuildApprovalsMergeSql(chunk.Count);
            await using var cmd = new SqlCommand(sql, conn, tx);
            cmd.Parameters.Add("@envId", SqlDbType.BigInt).Value = envId;
            cmd.Parameters.Add("@approvalType", SqlDbType.VarChar, 4).Value = approvalType;

            for (var i = 0; i < chunk.Count; i++)
            {
                var a = chunk[i];
                cmd.Parameters.Add($"@id{i}", SqlDbType.Int).Value = a.ApprovalId;
                cmd.Parameters.Add($"@rank{i}", SqlDbType.Int).Value = a.Rank;
                cmd.Parameters.Add($"@attempt{i}", SqlDbType.Int).Value = a.Attempt;
                cmd.Parameters.Add($"@status{i}", SqlDbType.NVarChar, 50).Value = DbVal(NullIfEmpty(a.Status));
                cmd.Parameters.Add($"@automated{i}", SqlDbType.Bit).Value = a.IsAutomated;
                cmd.Parameters.Add($"@approverId{i}", SqlDbType.NVarChar, 255).Value = DbVal(a.ApproverId);
                cmd.Parameters.Add($"@approverName{i}", SqlDbType.NVarChar, 255).Value = DbVal(a.ApproverDisplayName);
                cmd.Parameters.Add($"@approvedByName{i}", SqlDbType.NVarChar, 255).Value = DbVal(a.ApprovedByDisplayName);
                cmd.Parameters.Add($"@comment{i}", SqlDbType.NVarChar, 1000).Value = DbVal(a.Comment);
                cmd.Parameters.Add($"@modifiedOn{i}", SqlDbType.DateTime).Value = DbVal(a.ModifiedOnUtc);
                cmd.Parameters.Add($"@raw{i}", SqlDbType.NVarChar, -1).Value = a.RawJson;
            }

            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    private static IEnumerable<List<AzureApprovalItem>> ApprovalsChunks(IReadOnlyList<AzureApprovalItem> approvals)
    {
        const int perRowParams = 12;
        var maxRows = Math.Max(1, (SqlServerMaxParameterCount - 100) / perRowParams);

        for (var i = 0; i < approvals.Count; i += maxRows)
            yield return approvals.Skip(i).Take(maxRows).ToList();
    }

    private static string BuildApprovalsMergeSql(int rowCount)
    {
        var rows = new StringBuilder();
        for (var i = 0; i < rowCount; i++)
        {
            if (i > 0)
                rows.Append(',');
            rows.Append($"(CAST(@id{i} AS INT), CAST(@rank{i} AS INT), CAST(@attempt{i} AS INT), " +
                        $"CAST(@status{i} AS NVARCHAR(50)), CAST(@automated{i} AS BIT), " +
                        $"CAST(@approverId{i} AS NVARCHAR(255)), CAST(@approverName{i} AS NVARCHAR(255)), " +
                        $"CAST(@approvedByName{i} AS NVARCHAR(255)), CAST(@comment{i} AS NVARCHAR(1000)), " +
                        $"CAST(@modifiedOn{i} AS DATETIME), CAST(@raw{i} AS NVARCHAR(MAX)))");
        }

        return $"""
            MERGE dbo.AzureReleaseApproval WITH (HOLDLOCK) AS t
            USING (VALUES {rows}) AS s
                (ApprovalId, Rank, Attempt, Status, IsAutomated, ApproverId,
                 ApproverDisplayName, ApprovedByDisplayName, Comment, ModifiedOnUtc, RawJson)
                ON t.AzureReleaseEnvId = @envId
               AND t.ApprovalType = @approvalType
               AND t.Attempt = s.Attempt
               AND t.Rank = s.Rank
            WHEN MATCHED THEN UPDATE SET
                ApprovalId            = s.ApprovalId,
                Status                = s.Status,
                IsAutomated           = s.IsAutomated,
                ApproverId            = s.ApproverId,
                ApproverDisplayName   = s.ApproverDisplayName,
                ApprovedByDisplayName = COALESCE(s.ApprovedByDisplayName, t.ApprovedByDisplayName),
                Comment               = COALESCE(s.Comment, t.Comment),
                ModifiedOnUtc         = COALESCE(s.ModifiedOnUtc, t.ModifiedOnUtc),
                RawJson               = s.RawJson,
                LastSyncAtUtc         = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT (
                AzureReleaseEnvId, ApprovalType, ApprovalId, Rank, Attempt, Status,
                IsAutomated, ApproverId, ApproverDisplayName, ApprovedByDisplayName,
                Comment, ModifiedOnUtc, RawJson, FirstSyncAtUtc, LastSyncAtUtc)
            VALUES (
                @envId, @approvalType, s.ApprovalId, s.Rank, s.Attempt, s.Status,
                s.IsAutomated, s.ApproverId, s.ApproverDisplayName, s.ApprovedByDisplayName,
                s.Comment, s.ModifiedOnUtc, s.RawJson, SYSUTCDATETIME(), SYSUTCDATETIME());
            """;
    }

    private static object DbVal(object? value) => value ?? DBNull.Value;

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
