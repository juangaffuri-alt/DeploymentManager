using PasadorScript.DTO.AzureDev;
using PasadorScript.DTO.Config;
using PasadorScript.Models.Reports;
using QuestPDF.Infrastructure;
using System.Collections;

namespace PasadorScript.Extensions
{
    public static class ConfiguationExtend
    {
        public static ICollection<ProjectSetting> GetProjectSettings(this IConfiguration configuration, bool? gx, bool? active, bool? ownBuild, string? repository)
        {
            var projects = configuration.GetSection("Projects").Get<IEnumerable<ProjectSetting>>();
            if (!projects!.Any())
            {
                return new List<ProjectSetting>();
            }
            if (gx.HasValue)
                projects = projects!.Where(x => x.IsGx == gx.Value);
            if (active.HasValue)
                projects = projects!.Where(x => x.Active == active.Value);
            if (ownBuild.HasValue)
                projects = projects!.Where(x => x.Repos!.Any(y => y.OwnBuild == ownBuild.Value));
            if (!string.IsNullOrEmpty(repository))
                projects = projects!.Where(x => x.Repos!.Any(y => y.RepoName == repository));
            return projects!.ToList();

        }

        public static ICollection<ReviewerConfig> GetReviewerConfig(this IConfiguration configuration, string? groupDescription)
        {
            var rvws = configuration.GetSection("AzureSettings:Reviewers").Get<IEnumerable<ReviewerConfig>>();
            if (!string.IsNullOrEmpty(groupDescription))
            {
                rvws = rvws!.Where(x=>x.Descripcion.Equals(groupDescription, StringComparison.InvariantCultureIgnoreCase));
            }
            return rvws!.ToList();
        }
        
    }

}


