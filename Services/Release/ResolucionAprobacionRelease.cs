using PasadorScript.DTO.AzureDev;

namespace PasadorScript.Services.Release;

/// <summary>
/// Cruza el rol de la sesión con <c>AzureSettings:DescriptorsGroup</c> y la fase del stage.
/// </summary>
public class ResolucionAprobacionRelease : IResolucionAprobacionRelease
{
    public const string RolQa = "QA";
    public const string RolIyc = "IYC";
    public const string RolCoordinadores = "Coordinadores";

    private static readonly string[] RolesProduccion = [RolCoordinadores, RolIyc];
    private static readonly string[] RolesResto = [RolQa, RolIyc];

    private readonly IConfiguration _configuration;

    public ResolucionAprobacionRelease(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <inheritdoc />
    public IReadOnlyList<ResolucionStage> Resolver(ReleaseDto release, IReadOnlyCollection<string> rolesSesion)
    {
        if (release.Environments.Count == 0)
            return [];

        var roles = NormalizarRoles(rolesSesion);
        var descriptoresPorRol = LeerDescriptores();
        var nombresPorRol = LeerNombresGrupo();
        var stages = new List<ResolucionStage>();

        foreach (var env in release.Environments)
        {
            if (!MuestraAcciones(env))
                continue;

            var aprobaciones = FiltrarFase(env, roles, descriptoresPorRol, nombresPorRol);
            if (aprobaciones.Count == 0)
                continue;

            stages.Add(new ResolucionStage
            {
                EnvironmentId = env.Id,
                IncluyeSinPrueba = aprobaciones.Any(a => a.PermiteSinPrueba),
                Aprobaciones = aprobaciones
            });
        }

        return stages;
    }

    /// <inheritdoc />
    public AprobacionResuelta? BuscarPermitida(ReleaseDto release, IReadOnlyCollection<string> rolesSesion, int approvalId)
    {
        if (approvalId <= 0)
            return null;

        foreach (var stage in Resolver(release, rolesSesion))
        {
            foreach (var aprobacion in stage.Aprobaciones)
            {
                if (aprobacion.Id == approvalId)
                    return aprobacion;
            }
        }

        return null;
    }

    private List<AprobacionResuelta> FiltrarFase(
        EnvironmentDto env,
        HashSet<string> roles,
        Dictionary<string, string> descriptoresPorRol,
        Dictionary<string, string> nombresPorRol)
    {
        var rolesValidos = env.IsProduction ? RolesProduccion : RolesResto;
        var descriptores = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nombres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rol in rolesValidos)
        {
            if (!roles.Contains(rol))
                continue;

            if (descriptoresPorRol.TryGetValue(rol, out var descriptor))
                descriptores.Add(descriptor);
            if (nombresPorRol.TryGetValue(rol, out var nombre))
                nombres.Add(nombre);
        }

        if (descriptores.Count == 0 && nombres.Count == 0)
            return [];

        // Azure deja el stage en inProgress mientras la previa sigue pendiente.
        // La posterior entra solo cuando ya no hay una previa pendiente.
        var esPosterior = !env.PreDeployApprovals.Any(EstaPendiente);
        var fuente = esPosterior ? env.PostDeployApprovals : env.PreDeployApprovals;
        var permiteSinPrueba = esPosterior && !env.IsProduction && roles.Contains(RolQa);

        return fuente
            .Where(a => EstaPendiente(a) && CoincideIdentidad(a, descriptores, nombres))
            .Select(a => new AprobacionResuelta
            {
                Id = a.Id,
                DisplayName = a.Approver?.DisplayName ?? "",
                Descriptor = a.Approver?.Descriptor ?? "",
                EsPosterior = esPosterior,
                PermiteSinPrueba = permiteSinPrueba
            })
            .ToList();
    }

    /// <summary>
    /// DescriptorsGroup guarda el id o el descriptor. El grupo de QA en el release
    /// llega como <c>[TEAM FOUNDATION]\DevOps_BPNQA</c>, configurado en GruposAprobacion.
    /// </summary>
    private static bool CoincideIdentidad(Approvers aprobacion, HashSet<string> identidades, HashSet<string> nombres)
    {
        var descriptor = aprobacion.Approver?.Descriptor;
        if (!string.IsNullOrWhiteSpace(descriptor) && identidades.Contains(descriptor))
            return true;

        var id = aprobacion.Approver?.Id;
        if (!string.IsNullOrWhiteSpace(id) && identidades.Contains(id))
            return true;

        return nombres.Count > 0
            && (EsMismoGrupo(aprobacion.Approver?.DisplayName, nombres)
                || EsMismoGrupo(aprobacion.Approver?.UniqueName, nombres));
    }

    /// <summary>
    /// El unique name de un grupo de Team Foundation trae el prefijo <c>[TEAM FOUNDATION]\</c>.
    /// </summary>
    private static bool EsMismoGrupo(string? valor, HashSet<string> nombres)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return false;

        if (nombres.Contains(valor))
            return true;

        var barra = valor.LastIndexOf('\\');
        return barra >= 0 && nombres.Contains(valor[(barra + 1)..]);
    }

    private Dictionary<string, string> LeerDescriptores()
    {
        var configurados = _configuration
            .GetSection("AzureSettings:DescriptorsGroup")
            .Get<Dictionary<string, string>>();

        var porRol = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (configurados == null)
            return porRol;

        foreach (var item in configurados)
        {
            if (string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.Value))
                continue;

            porRol[item.Key.Trim()] = item.Value.Trim();
        }

        return porRol;
    }

    private Dictionary<string, string> LeerNombresGrupo()
    {
        var configurados = _configuration
            .GetSection("AzureSettings:GruposAprobacion")
            .Get<Dictionary<string, string>>();

        var porRol = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (configurados == null)
            return porRol;

        foreach (var item in configurados)
        {
            if (string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.Value))
                continue;

            porRol[item.Key.Trim()] = item.Value.Trim();
        }

        // El rol de la sesión es Coordinadores. GruposAprobacion guarda el nombre
        // de Azure de ese rol bajo la clave Supervisor (DevOps_DM_Supervisor).
        if (!porRol.ContainsKey(RolCoordinadores)
            && porRol.TryGetValue("Supervisor", out var grupoSupervisor))
        {
            porRol[RolCoordinadores] = grupoSupervisor;
        }

        return porRol;
    }

    /// <summary>
    /// Pending o inProgress, el mismo conjunto que el if de acciones de ViewStatus.
    /// </summary>
    private static bool MuestraAcciones(EnvironmentDto env) =>
        env.Status is "pending" or "inProgress";

    private static bool EstaPendiente(Approvers aprobacion) =>
        string.Equals(aprobacion.Status, "pending", StringComparison.OrdinalIgnoreCase);

    private static HashSet<string> NormalizarRoles(IReadOnlyCollection<string>? rolesSesion)
    {
        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (rolesSesion == null)
            return roles;

        foreach (var rol in rolesSesion)
        {
            if (!string.IsNullOrWhiteSpace(rol))
                roles.Add(rol.Trim());
        }

        return roles;
    }
}
