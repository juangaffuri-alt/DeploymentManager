using PasadorScript.Services.TeamDev;
using PasadorScript.ViewModels;

namespace PasadorScript.DTO.TeamDev
{
    public class Action
    {
        public string type { get; set; }
        public string objectGuid { get; set; }
        public string objectType { get; set; }
        public string objectTypeGuid { get; set; }
        public string objectName { get; set; }
        public string objectDescription { get; set; }
    }

    public class CommitsTeamDev
    {
        public IList<BuildInCommit> Builds { get; set; }
        public int revision { get; set; }
        public string author { get; set; }
        public string date { get; set; }
        public List<Action> actions { get; set; }
        public string message { get; set; }
    }
}
