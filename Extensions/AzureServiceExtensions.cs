using Microsoft.Extensions.Logging;
using PasadorScript.DTO.AzureDev;
using PasadorScript.Services.Azure;

namespace PasadorScript.Extensions
{
    public static class AzureServiceExtensions
    {
        public const string WarningReleaseNoCancelado =
            "El envío se anuló, pero no se pudo cancelar el Release en Azure DevOps.";

        private const bool UsarPatSistema = true;

        // Método genérico para cualquier interfaz Azure
        public static T SetProjectType<T>(this T azureService, AzureProjectType projectType) where T : class
        {
            if (azureService == null)
                throw new ArgumentNullException(nameof(azureService));

            if (azureService is AzureBaseService baseService)
            {
                baseService.ProjectType = projectType;
                return azureService;
            }

            throw new InvalidOperationException(
                $"El servicio {azureService.GetType().Name} no hereda de AzureBaseService.");
        }

        /// <summary>
        /// Al anular: rechaza approvals pendientes (incluye PRO pendiente de IYC),
        /// cancela stages activos no-PROD y abandona el Release con PAT de sistema
        /// (el aprobador de PRO suele ser IYC; el usuario que anula no puede rechazar).
        /// Fail-soft: no debe impedir soft-delete ni abandonar el PR.
        /// Requiere ForceProyectType previo (igual que HasDeployedInProdAsync).
        /// </summary>
        /// <returns>false si había Release y no se pudo cancelar/abandonar en Azure.</returns>
        public static async Task<bool> CancelarDeployActivoNoProdAsync(
            this IAzureService azureService,
            int pullRequestId,
            ILogger logger)
        {
            try
            {
                var release = await azureService.ObtenerReleaseDesdePullRequestAsync(pullRequestId);
                if (release == null)
                    return true;

                await RechazarApprovalsPendientesAsync(azureService, release, pullRequestId, logger);
                await CancelarEnvironmentsPendientesOActivosAsync(azureService, release, pullRequestId, logger);

                var abandoned = await azureService.AbandonarReleaseAsync(
                    release.Id, "Anulado por el usuario", UsarPatSistema);
                if (!abandoned)
                {
                    logger.LogWarning(
                        "No se pudo abandonar el release {ReleaseId} para PR {PrId}",
                        release.Id, pullRequestId);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error cancelando deploy activo al descartar PR {PrId}", pullRequestId);
                return false;
            }
        }

        private static bool EsEnvironmentActivo(string? status) =>
            string.Equals(status, "inProgress", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "queued", StringComparison.OrdinalIgnoreCase);

        private static async Task RechazarApprovalsPendientesAsync(
            IAzureService azureService,
            ReleaseDto release,
            int pullRequestId,
            ILogger logger)
        {
            var pendientes = (release.Environments ?? [])
                .SelectMany(e =>
                    (e.PreDeployApprovals ?? []).Concat(e.PostDeployApprovals ?? [])
                        .Where(a => string.Equals(a.Status, "pending", StringComparison.OrdinalIgnoreCase))
                        .Select(a => (Env: e.Name, ApprovalId: a.Id)))
                .ToList();

            foreach (var (env, approvalId) in pendientes)
            {
                var result = await azureService.ActualizarEstadoEnvironmentAsync(
                    approvalId,
                    ReleaseEnvironmentAction.Reject,
                    "Anulado por el usuario",
                    UsarPatSistema);

                if (!result.Success)
                {
                    logger.LogWarning(
                        "No se pudo rechazar approval {ApprovalId} del env {Env} para PR {PrId}: {Error}",
                        approvalId, env, pullRequestId, result.ErrorMessage);
                }
            }
        }

        private static async Task CancelarEnvironmentsPendientesOActivosAsync(
            IAzureService azureService,
            ReleaseDto release,
            int pullRequestId,
            ILogger logger)
        {
            var aCancelar = (release.Environments ?? [])
                .Where(e => e.HasPendingApproval || (!e.IsProduction && EsEnvironmentActivo(e.Status)))
                .ToList();

            foreach (var env in aCancelar)
            {
                var canceled = await azureService.CancelarEnvironmentAsync(
                    release.Id, env.Id, "Anulado por el usuario", UsarPatSistema);
                if (!canceled)
                {
                    logger.LogWarning(
                        "No se pudo cancelar environment {EnvironmentId} ({Env}) del release {ReleaseId} para PR {PrId}",
                        env.Id, env.Name, release.Id, pullRequestId);
                }
            }
        }
    }
}
