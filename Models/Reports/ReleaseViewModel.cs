using PasadorScript.Helpers;
using X.PagedList;

namespace PasadorScript.Models.Reports
{
    public class ReleaseViewModel
    {
        public string ReleaseDefinition { get; set; }  // PRO (BT/BTS)
        public string ReleaseName { get; set; }         // Release-679
        public string Stage { get; set; }              
        public int ReleaseId { get; set; }              // 12564
        public string Build { get; set; }               // main (mostrar con ícono)
        public string Branch { get; set; }              // main
        public string DeployedBy { get; set; }           // Vargas, Cecilia Mariel
        public DateTime QueuedOn { get; set; }           // 18/3/2026, 1'
        public DateTime QueuedOnLocal => QueuedOn.ToArgentinaTime();
    }
    
    
    public class ReleaseDefinition
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
    public class ProjectConfig
    {
        public string DisplayName { get; set; }
        public string ProjectName { get; set; }
        public bool IsGx { get; set; }
        public List<ReleaseDefinitionConfig> ReleaseDefinitions { get; set; }
    }

    public class ReleaseDefinitionConfig
    {
        public int DefinitionId { get; set; }
        public string PipelineName { get; set; }
    }
}
