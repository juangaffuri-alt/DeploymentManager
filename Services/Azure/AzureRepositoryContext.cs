using PasadorScript.DTO.AzureDev;

namespace PasadorScript.Services.Azure
{
    public class AzureRepositoryContext
    {
        public string VsspsHost { get; private set; }
        public string VsrmHost { get; private set; }
        public string AzureDevopsHost { get; private set; }
        public string Organization { get; private set; }
        public string Project { get; private set; }
        public string RepositoryId { get; set; }
        public int? BuildDefinitionId { get; private set; }

        public AzureRepositoryContext(string host, string organization, string project, string repoId, string vsrmHost, string vsspsHost, int? buildDefinitionId = null)
        {
            AzureDevopsHost = host ?? throw new ArgumentNullException(nameof(host));
            Organization = organization ?? throw new ArgumentNullException(nameof(organization));
            Project = project ?? throw new ArgumentNullException(nameof(project));
            RepositoryId = repoId;
            BuildDefinitionId = buildDefinitionId;
            VsrmHost = vsrmHost;
            VsspsHost = vsspsHost;
        }

        public static AzureRepositoryContext FromConfig(IConfiguration cfg, AzureProjectType type)
        {
            string project = "";
            string repository = "";
            int? defId = null;

            var pipelinesConfig = cfg.GetSection("Projects").Get<List<ProjectSetting>>();

            if (type == AzureProjectType.Default)
            {
                var scriptProj = pipelinesConfig.FirstOrDefault(x=> !x.IsGx && x.Active);
                project = scriptProj.ProjectName;
                repository = scriptProj.ProjectName;
            }
            else if (type == AzureProjectType.GeneXus)
            {
                var gxProj = pipelinesConfig.FirstOrDefault(x => x.IsGx && x.Active);
                project = gxProj.ProjectName;
                repository = gxProj.ProjectName;
                defId = gxProj.DefaultReleaseDefinitionId;
            }
            else if (type == AzureProjectType.ScriptOld)
            {
                var scriptProj = pipelinesConfig.FirstOrDefault(x => !x.IsGx && !x.Active);
                project = scriptProj.ProjectName;
                repository = scriptProj.ProjectName;
            }
            else if (type == AzureProjectType.GenexusOld)
            {
                var gxProj = pipelinesConfig.FirstOrDefault(x => x.IsGx && !x.Active);
                project = gxProj.ProjectName;
                repository = gxProj.ProjectName;
                defId = gxProj.DefaultReleaseDefinitionId;
            }
            else
            {
                throw new ArgumentException($"Unsupported AzureProjectType: {type}");
            }            

            return new AzureRepositoryContext(
                cfg["AzureSettings:Host"]!,
                cfg["AzureSettings:Organization"]!,
                project,
                repository,
                cfg["AzureSettings:EdpointVSRM:Host"]!,
                cfg["AzureSettings:EndpointsGraph:Host"]!,
                defId.HasValue ? defId.Value : null
            );
        }

        public static AzureRepositoryContext FromCustomProject(IConfiguration cfg, string projectName)
        {
            string repository = "AzureSettings:Project";

            return new AzureRepositoryContext(
                cfg["AzureSettings:Host"]!,
                cfg["AzureSettings:Organization"]!,
                projectName,
                cfg[$"{repository}"]!,
                cfg["AzureSettings:EdpointVSRM:Host"]!,
                cfg["AzureSettings:EndpointsGraph:Host"]!,
                null
            );
        }

        public string BaseUrl => $"{AzureDevopsHost}/{Organization}/{Uri.EscapeDataString(Project)}";
        public string VsrmUrl => $"{VsrmHost}/{Organization}/{Uri.EscapeDataString(Project)}";
        public string GraphUrl => $"{VsspsHost}/{Organization}/{Uri.EscapeDataString(Project)}";
        public override string ToString() => $"{Organization}/{Uri.EscapeDataString(Project)}/{Uri.EscapeDataString(RepositoryId)}";
    }
}
