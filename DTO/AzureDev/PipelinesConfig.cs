using PasadorScript.Helpers;
using System.Text.Json.Serialization;

namespace PasadorScript.DTO.AzureDev
{

    public class ProjectSetting
    {
        public int? DefaultReleaseDefinitionId { get; set; }
        public string DisplayName { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public List<RepoSetting> Repos { get; set; } = new List<RepoSetting>();
        public bool IsGx { get; set; }
        public bool Active { get; set; }
        public string PipelineBuildNameGx { get; set; } = "";
        public string PipelineBuildTaskName { get; set; } = "";
    }

    public class RepoSetting
    {
        public int? BuildDefinitionId { get; set; }
        public int ReleaseDefinitionId { get; set; }
        public string PipelineName { get; set; } = "";
        public string RepoName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Param { get; set; } = "";
        public string PipelineDescription { get; set; } = "";
        public bool? OwnBuild { get; set; }
    }

    public class PipelinesConfig
    {
        public string PipelineName { get; set; } = "";
        public int DefinitionId { get; set; }
        public string RepoName { get; set; } = "";
    }

}
