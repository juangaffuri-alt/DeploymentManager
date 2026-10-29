using PasadorScript.Helpers;

namespace PasadorScript.ViewModels
{
    public class FileView
    {
        public string DisplayName { get; set; }
        public string IdFileAzure { get; set; }
        public string FileName { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string Content { get; set; }
        public int IdEstado { get; set; }
        public string DescripcionEstado { get; set; }
        public string Path { get; set; }
        public string Origin { get; set; }
        public string TemplateType { get; set; }
        public Helpers.SqlStructureInfo ParsedStructure { get; set; }
    }
    public class PullRequestViewModel
    {
        public int PullRequestId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public string RepositoryName { get; set; }
        public string SourceBranch { get; set; }
        public string TargetBranch { get; set; }
        public List<FileChangeViewModel> FileChanges { get; set; } = new List<FileChangeViewModel>();
    }

    public class FileChangeViewModel
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string ChangeType { get; set; }
        public string ChangeTypeDisplay => GetChangeTypeDisplay();
        public string ChangeTypeClass => GetChangeTypeClass();
        public int AddedLines { get; set; }
        public int DeletedLines { get; set; }
        public List<DiffLineViewModel> DiffLines { get; set; } = new List<DiffLineViewModel>();
        public bool HasDiff => DiffLines.Any();

        private string GetChangeTypeDisplay()
        {
            return ChangeType switch
            {
                "add" => "Added",
                "delete" => "Deleted",
                "edit" => "Modified",
                "rename" => "Renamed",
                _ => ChangeType
            };
        }

        private string GetChangeTypeClass()
        {
            return ChangeType switch
            {
                "add" => "text-success",
                "delete" => "text-danger",
                "edit" => "text-warning",
                "rename" => "text-info",
                _ => "text-muted"
            };
        }
    }

    public class DiffLineViewModel
    {
        public int LineNumber { get; set; }
        public int? OldLineNumber { get; set; }
        public int? NewLineNumber { get; set; }
        public string Content { get; set; }
        public DiffLineType Type { get; set; }
        public string TypeClass => GetTypeClass();

        private string GetTypeClass()
        {
            return Type switch
            {
                DiffLineType.Added => "diff-added",
                DiffLineType.Deleted => "diff-deleted",
                DiffLineType.Context => "diff-context",
                _ => ""
            };
        }
    }

    public enum DiffLineType
    {
        Context = 0,
        Added = 1,
        Deleted = 2
    }
}
