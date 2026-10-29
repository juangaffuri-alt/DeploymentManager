using PasadorScript.DTO.AzureDev;

namespace PasadorScript.Helpers
{
    public class EnvironmentHelper
    {
        public static void UpdateProjectType(ICollection<EnvironmentDto> environments, string projectType)
        {
            foreach (var e in environments)
            {
                e.AzureProjectType = projectType;
            }
        }

        public static void UpdateVisible(ICollection<EnvironmentDto> environments, bool visible)
        {
            foreach (var e in environments)
            {
                e.Visible = visible;
            }
        }
    }
}
