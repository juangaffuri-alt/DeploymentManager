namespace AzureReleasesSync;

/// <summary>
/// Resultado de una corrida de sincronización (invariante: Success == Errors == 0 &amp;&amp; !Truncated).
/// </summary>
public sealed record SyncRunResult(
    int ReleasesUpserted,
    int DefinitionsTotal,
    int DefinitionsOk,
    int Errors,
    bool Truncated)
{
    public bool Success => Errors == 0 && !Truncated;
}
