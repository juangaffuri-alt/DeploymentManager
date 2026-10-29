using System.Globalization;

namespace AzureReleasesSync;

/// <summary>
/// Configuración del sincronizador leída de appsettings.json (y sobrescribible por variables de entorno).
/// </summary>
public sealed class SyncOptions
{
    /// <summary>Connection string hacia DeploymentManager_DF (misma DB de la aplicación).</summary>
    public required string ConnectionString { get; init; }

    /// <summary>Ej.: https://vsrm.dev.azure.com</summary>
    public required string VsrmHost { get; init; }

    /// <summary>Organización de Azure DevOps. Ej.: Banco-Provincia-Neuquen</summary>
    public required string Organization { get; init; }

    /// <summary>PAT de servicio con permiso Release (lectura).</summary>
    public required string PersonalAccessToken { get; init; }

    /// <summary>Definiciones a sincronizar: (ProjectName, ReleaseDefinitionId).</summary>
    public required IReadOnlyList<AzureDefinitionRef> Definitions { get; init; }

    /// <summary>Días de solapamiento en la corrida incremental (re-sincroniza releases recientes).</summary>
    public int OverlapDays { get; init; } = 7;

    /// <summary>Días hacia atrás del backfill inicial cuando no hay watermark exitoso previo.</summary>
    public int BackfillDays { get; init; } = 364;

    /// <summary>Cuántos releases como máximo deben caber en una página sin truncar (MaxPages x PageSize).</summary>
    public int MaxReleasesPerRunPerDefinition => MaxPagesPerDefinition * PageSize;

    /// <summary>Tope de páginas por definición por corrida (mismo criterio que el informe gerencial).</summary>
    public int MaxPagesPerDefinition { get; init; } = 20;

    /// <summary>Tamaño de página de la API de release ($top).</summary>
    public int PageSize { get; init; } = 100;

    /// <summary>Concurrencia máxima de llamadas a Azure.</summary>
    public int MaxDegreeOfParallelism { get; init; } = 4;

    public static SyncOptions Load(IConfiguration config)
    {
        var conn = config.GetConnectionString("NuevoP229")
            ?? throw new InvalidOperationException("Falta ConnectionStrings:NuevoP229 en appsettings.json.");

        var host = config["AzureSettings:Host"] is { Length: > 0 } h ? h : "https://dev.azure.com";
        var vsrm = config["AzureSettings:EdpointVSRM:Host"];
        if (string.IsNullOrWhiteSpace(vsrm))
        {
            // Deriva el host VSRM desde el host principal: https://dev.azure.com -> https://vsrm.dev.azure.com
            vsrm = host.Contains("dev.azure.com", StringComparison.OrdinalIgnoreCase)
                ? host.Replace("dev.azure.com", "vsrm.dev.azure.com", StringComparison.OrdinalIgnoreCase)
                : host;
        }

        var org = config["AzureSettings:Organization"]
            ?? throw new InvalidOperationException("Falta AzureSettings:Organization en appsettings.json.");

        var pat = config["AzureSettings:PersonalAccessToken"]
            ?? throw new InvalidOperationException("Falta AzureSettings:PersonalAccessToken en appsettings.json.");

        var definitions = new List<AzureDefinitionRef>();
        foreach (var project in config.GetSection("Projects").GetChildren())
        {
            var projectName = project["ProjectName"];
            if (string.IsNullOrWhiteSpace(projectName))
                continue;

            foreach (var repo in project.GetSection("Repos").GetChildren())
            {
                var rawDefId = repo["ReleaseDefinitionId"];
                if (!int.TryParse(rawDefId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var defId) || defId <= 0)
                    continue;

                definitions.Add(new AzureDefinitionRef(projectName, defId));
            }
        }

        definitions = definitions.Distinct().ToList();
        if (definitions.Count == 0)
            throw new InvalidOperationException("No se encontraron definiciones de release en la sección 'Projects' de appsettings.json.");

        return new SyncOptions
        {
            ConnectionString = conn,
            VsrmHost = vsrm.TrimEnd('/'),
            Organization = org,
            PersonalAccessToken = pat,
            Definitions = definitions,
            OverlapDays = ReadInt(config, "Sync:OverlapDays", 7),
            BackfillDays = ReadInt(config, "Sync:BackfillDays", 364),
            MaxPagesPerDefinition = ReadInt(config, "Sync:MaxPagesPerDefinition", 20),
            PageSize = ReadInt(config, "Sync:PageSize", 100),
            MaxDegreeOfParallelism = Math.Max(1, ReadInt(config, "Sync:MaxDegreeOfParallelism", 4)),
        };
    }

    private static int ReadInt(IConfiguration config, string key, int fallback) =>
        int.TryParse(config[key], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;
}

/// <summary>Par proyecto/definición de release de Azure DevOps.</summary>
public sealed record AzureDefinitionRef(string ProjectName, int ReleaseDefinitionId);
