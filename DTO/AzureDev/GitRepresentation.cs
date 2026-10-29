using System.Text.Json.Serialization;

namespace PasadorScript.DTO.AzureDev
{
    public class GitItemsResponse
    {
        public List<GitFileItem> Value { get; set; } = new();
        public int Count { get; set; }
    }

    public class GitFileItem
    {
        public string ObjectId { get; set; } = "";
        public string GitObjectType { get; set; } = "";
        public string Path { get; set; } = "";
        public bool IsFolder { get; set; }
        public string Url { get; set; } = "";
        public long Size { get; set; }

        [JsonPropertyName("commitId")]
        public string CommitId { get; set; } = "";

        // Para obtener fecha de modificación necesitarías hacer otra llamada a commits
        public DateTime LastModified { get; set; } = DateTime.Now;
        public GitCommit? Author { get; set; } = null;
    }

    public class GitCommit
    {
        public string CommitId { get; set; } = "";
        public string Comment { get; set; } = "";
        public List<string> Parents { get; set; }
        public GitCommitAuthor Author { get; set; }
    }

    public class GitCommitAuthor
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public DateTime Date { get; set; }
    }
    public class GitRef
    {
        public string Name { get; set; }
    }

    public class GitCommitDiffs
    {
        public List<GitFileDiff> Changes { get; set; }
    }

    public class GitFileDiff
    {
        public GitFileItem Item { get; set; }
        public string ChangeType { get; set; }
    }

    public class GitFileDiffResponse
    {
        public List<GitFileDiffBlock> DiffBlocks { get; set; }
    }

    public class GitFileDiffBlock
    {
        public int ChangeType { get; set; }
        public GitPosition ModifiedPosition { get; set; }
        public GitPosition OriginalPosition { get; set; }
        public List<GitDiffLine> Lines { get; set; }
    }

    public class GitPosition
    {
        public int Line { get; set; }
        public int Offset { get; set; }
    }

    public class GitDiffLine
    {
        public int LineType { get; set; } // 1 = Added, 2 = Deleted, 3 = Context
        public string Text { get; set; }
        public GitPosition SourceLine { get; set; }
        public GitPosition TargetLine { get; set; }
    }
    public class GitCommitsResponse
    {
        [JsonPropertyName("value")]
        public List<GitCommit> Value { get; set; }
    }
}
