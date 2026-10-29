using PasadorScript.DTO.AzureDev;

namespace PasadorScript.ViewModels
{
    public class BuildTimelineViewModel
    {
        public int BuildId { get; set; }
        public string AzureProjectType { get; set; } = "Genexus";
        public List<TaskInfo> Tasks { get; set; } = new();
    }
}
