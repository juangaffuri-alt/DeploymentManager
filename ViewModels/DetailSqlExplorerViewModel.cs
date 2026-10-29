using PasadorScript.DTO.AzureDev;

namespace PasadorScript.ViewModels
{
    public class SqlStructureInfo
    {
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Schema { get; set; } = "dbo";
        public bool IsStructure { get; set; }
        public string Icon { get; set; } = string.Empty;
    }
    public class DetailSqlExplorerViewModel
    {
        public string RepositoryId { get; set; }
        public string? FilePath { get; set; }
        public List<CommitHistory> History { get; set; } = new List<CommitHistory>();
        public string? FileContent { get; set; }
        // Nueva propiedad para la estructura SQL
        public SqlStructureInfo SqlStructure { get; set; }

        // Propiedades calculadas para fácil acceso en la vista
        public string DisplayName => SqlStructure?.IsStructure == true
            ? $"{SqlStructure.Schema}.{SqlStructure.Name}"
            : System.IO.Path.GetFileName(FilePath);

        public string TypeIcon => SqlStructure?.Icon ?? "<i class=\"fas fa-file-code text-secondary\"></i>";
        public string TypeBadge => SqlStructure?.IsStructure == true
            ? $"<span class=\"badge bg-light text-dark\">{SqlStructure.Type}</span>"
            : "";

        public string TypeDisplayName => SqlStructure?.IsStructure == true
            ? $"{SqlStructure.Type}"
            : "NA";
    }
}
