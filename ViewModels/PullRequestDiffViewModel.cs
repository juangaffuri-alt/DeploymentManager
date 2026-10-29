using PasadorScript.DTO.AzureDev;

namespace PasadorScript.ViewModels
{
    // View Models
    public class PRViewModel
    {
        public int PRId { get; set; }
        public PRDetails PRDetails { get; set; }
        public List<PRFileChange> Files { get; set; }
        public Dictionary<string, FileDetailedDiff> FileDiffs { get; set; }
        public PRSummary Summary { get; set; }
        public string ViewMode { get; set; } // "inline", "sidebyside", "unified"
        public string SelectedFilePath { get; set; }
    }

    public class FileDiffPartialModel
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public List<DiffLine> DiffLines { get; set; }
        public string ViewMode { get; set; }
        public int AddedLines { get; set; }
        public int DeletedLines { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class MonacoDiffModel
    {
        public int PRId { get; set; }
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string OriginalContent { get; set; }
        public string ModifiedContent { get; set; }
        public List<DiffLine> DiffLines { get; set; }
        public string Language { get; set; }
        public int AddedLines { get; set; }
        public int DeletedLines { get; set; }
    }
    // Clase para el request POST
    public class MonacoDiffPostRequest
    {
        public int PrId { get; set; }
        public string FilePath { get; set; }
        public string ContentOrig { get; set; }
    }
}
