using PasadorScript.DTO.AzureDev;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PasadorScript.Services.Azure
{
    public abstract class AzureBaseService
    {
        protected readonly IConfiguration _configuration;

        public AzureProjectType ProjectType { get; set; }

        protected AzureRepositoryContext Ctx(AzureProjectType type)
            => AzureRepositoryContext.FromConfig(_configuration, type);

        protected AzureRepositoryContext Ctx(string projectName)
            => AzureRepositoryContext.FromCustomProject(_configuration, projectName);

        protected AzureBaseService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        protected string ExtractTaskLogFromFullLog(string fullLog, int taskId)
        {
            if (string.IsNullOrEmpty(fullLog))
                return $"No hay logs disponibles para la tarea {taskId}";

            // Intentar extraer solo las líneas relevantes a esta tarea
            var lines = fullLog.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var taskLines = new List<string>();
            bool inTaskSection = false;

            foreach (var line in lines)
            {
                // Buscar marcadores que indiquen el inicio de una tarea
                if (line.Contains($"Task: {taskId}") ||
                    line.Contains($"##[section]Starting:") ||
                    line.Contains("##[command]"))
                {
                    inTaskSection = true;
                    taskLines.Add(line);
                }
                else if (inTaskSection)
                {
                    // Si encontramos el inicio de otra tarea, salimos
                    if (line.Contains("##[section]Starting:") && taskLines.Count > 0)
                    {
                        break;
                    }
                    taskLines.Add(line);
                }
            }

            // Si no pudimos filtrar por tarea específica, devolver el log completo
            if (taskLines.Count == 0)
            {
                return fullLog;
            }

            return string.Join(Environment.NewLine, taskLines);
        }
        protected int GetMaxAttemptFromApprovals(JsonElement env)
        {
            int maxAttempt = 1;

            // Revisar pre-deploy approvals
            if (env.TryGetProperty("preDeployApprovals", out var preApprovals) &&
                preApprovals.ValueKind == JsonValueKind.Array)
            {
                foreach (var approval in preApprovals.EnumerateArray())
                {
                    if (approval.TryGetProperty("attempt", out var attemptProp))
                    {
                        int attempt = attemptProp.GetInt32();
                        if (attempt > maxAttempt)
                            maxAttempt = attempt;
                    }
                }
            }

            // Revisar post-deploy approvals
            if (env.TryGetProperty("postDeployApprovals", out var postApprovals) &&
                postApprovals.ValueKind == JsonValueKind.Array)
            {
                foreach (var approval in postApprovals.EnumerateArray())
                {
                    if (approval.TryGetProperty("attempt", out var attemptProp))
                    {
                        int attempt = attemptProp.GetInt32();
                        if (attempt > maxAttempt)
                            maxAttempt = attempt;
                    }
                }
            }

            return maxAttempt;
        }

        
    }
}
