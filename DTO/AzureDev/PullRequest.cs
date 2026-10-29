using PasadorScript.Helpers;
using System.Text.Json.Serialization;

namespace PasadorScript.DTO.AzureDev
{
    
    public class CreatedBy
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string UniqueName { get; set; } = "";
        public string Url { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public string Href { get; set; } = "";
        public string Descriptor { get; set; } = "";
    }

    public class Iterations
    {
        public string Href { get; set; } = "";
    }

    public class LastMergeCommit
    {
        public string CommitId { get; set; } = "";
        public Author Author { get; set; } = new();
        public Committer Committer { get; set; } = new();
        public string Comment { get; set; } = "";
        public string Url { get; set; } = "";
    }

    public class LastMergeSourceCommit
    {
        public string CommitId { get; set; } = "";
        public string Url { get; set; } = "";
    }

    public class LastMergeTargetCommit
    {
        public string CommitId { get; set; } = "";
        public string Url { get; set; } = "";
    }

    public class Links
    {
        public Self Self { get; set; } = new();
        public Repository Repository { get; set; } = new();
        public WorkItems WorkItems { get; set; } = new();
        public SourceBranch SourceBranch { get; set; } = new();
        public TargetBranch TargetBranch { get; set; } = new();
        public SourceCommit SourceCommit { get; set; } = new();
        public TargetCommit TargetCommit { get; set; } = new();
        public CreatedBy CreatedBy { get; set; } = new();
        public Iterations Iterations { get; set; } = new();
    }

    public class Project
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Url { get; set; } = "";
        public string State { get; set; } = "";
        public int Revision { get; set; }
    }

    public class RepositoryAzDev
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Url { get; set; } = "";
        public Project Project { get; set; } = new();   
        public string RemoteUrl { get; set; } = "";
        public string Href { get; set; } = "";
    }

    public class Reviewer
    {
        public string ReviewerUrl { get; set; } = "";
        public int Vote { get; set; }
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string UniqueName { get; set; } = "";
        public string Url { get; set; } = "";
        public string ImageUrl { get; set; } = "";
    }

    public class AzureDevopsPullRequest
    {
        public RepositoryAzDev Repository { get; set; } = new();
        public int PullRequestId { get; set; }
        public int CodeReviewId { get; set; }
        public string Status { get; set; } = "";
        public CreatedBy CreatedBy { get; set; } = new();
        public DateTime CreationDate { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string SourceRefName { get; set; } = "";
        public string TargetRefName { get; set; } = "";
        public string MergeStatus { get; set; } = "";
        public string MergeId { get; set; } = "";
        public LastMergeSourceCommit LastMergeSourceCommit { get; set; } = new();
        public LastMergeTargetCommit LastMergeTargetCommit { get; set; } = new();
        public LastMergeCommit LastMergeCommit { get; set; } = new();
        public List<Reviewer> Reviewers { get; set; } = new();
        public string Url { get; set; } = "";
        public Links Links { get; set; } = new();
        public bool SupportsIterations { get; set; }
        public string ArtifactId { get; set; } = "";
        public string StatusLabel => Status switch
        {
            "active" => "Activo",
            "completed" => "Completo",
            "abandoned" => "Abandonado",
            "canceled" => "Cancelado",
            _ => throw new NotImplementedException(),
        };
        public string StatusBadgeClass => Status switch
        {
            "active" => "badge badge-primary",
            "completed" => "badge badge-success",
            "abandoned" => "badge badge-danger",
            "canceled" => "badge badge-danger",
            _ => throw new NotImplementedException(),
        };

        // Propiedades calculadas esenciales
        [JsonIgnore]
        public string SourceBranch => SourceRefName?.Replace("refs/heads/", "") ?? string.Empty;

        [JsonIgnore]
        public string TargetBranch => TargetRefName?.Replace("refs/heads/", "") ?? string.Empty;

        [JsonIgnore]
        public bool IsCompleted => Status == "completed";

        [JsonIgnore]
        public bool HasMergeCommit => LastMergeCommit != null && !string.IsNullOrEmpty(LastMergeCommit.CommitId);
        [JsonIgnore]
        public DateTime CreationDateArgentina => CreationDate.ToArgentinaTime();

        [JsonIgnore]
        public string CreationDateArgentinaString => CreationDate.ToArgentinaTimeString();
    }

    public class PullRequestResponse
    {
        public List<AzureDevopsPullRequest> Value { get; set; } = new();
    }


    public class Self
    {
        public string href { get; set; } = "";
    }

    public class SourceBranch
    {
        public string href { get; set; } = "";
    }

    public class SourceCommit
    {
        public string href { get; set; } = "";
    }

    public class TargetBranch
    {
        public string href { get; set; } = "";
    }

    public class TargetCommit
    {
        public string href { get; set; } = "";
    }

    public class WorkItems
    {
        public string href { get; set; } = "";
    }

    // Modelos de datos
    public class PRDetails
    {
        [JsonPropertyName("pullRequestId")]
        public int PullRequestId { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("repository")]
        public RepositoryInfo Repository { get; set; }

        [JsonPropertyName("lastMergeSourceCommit")]
        public CommitInfo LastMergeSourceCommit { get; set; }

        [JsonPropertyName("lastMergeTargetCommit")]
        public CommitInfo LastMergeTargetCommit { get; set; }
    }

    public class RepositoryInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }
        public string DefaultBranch { get; set; }
    }

    public class CommitInfo
    {
        [JsonPropertyName("commitId")]
        public string CommitId { get; set; }
    }

    public class PRCompleteData
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public PRDetails PRDetails { get; set; }
        public List<PRFileChange> Files { get; set; } = new();
        public PRSummary Summary { get; set; }
    }

    public class PRSummary
    {
        public int TotalFilesChanged { get; set; }
        public int TotalLinesAdded { get; set; }
        public int TotalLinesDeleted { get; set; }
        public int AddedFiles { get; set; }
        public int ModifiedFiles { get; set; }
        public int DeletedFiles { get; set; }
    }

    public class PRFileChange
    {
        public string Path { get; set; }
        public string ChangeType { get; set; }
        public string ObjectId { get; set; }
        public FileChangeStats ChangeStats { get; set; }
        public string FileName => System.IO.Path.GetFileName(Path);
        public string Directory => System.IO.Path.GetDirectoryName(Path);

        public string ChangeTypeDescription => ChangeType switch
        {
            "add" => "Agregado",
            "edit" => "Modificado",
            "delete" => "Eliminado",
            _ => ChangeType
        };
    }

    public class FileChangeStats
    {
        public int Added { get; set; }
        public int Deleted { get; set; }
        public int Modified { get; set; }
        public int TotalChanges => Added + Deleted + Modified;
    }

    public class ThreadsResponse
    {
        [JsonPropertyName("value")]
        public List<Thread> Value { get; set; }
    }

    public class Thread
    {
        [JsonPropertyName("threadContext")]
        public ThreadContext ThreadContext { get; set; }
    }

    public class ThreadContext
    {
        [JsonPropertyName("filePath")]
        public string FilePath { get; set; }
    }

    public class IterationsResponse
    {
        [JsonPropertyName("value")]
        public List<Iteration> Value { get; set; }
    }

    public class Iteration
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
    }

    public class ChangesResponse
    {
        [JsonPropertyName("changeEntries")]
        public List<ChangeEntry> ChangeEntries { get; set; }
    }

    public class ChangeEntry
    {
        [JsonPropertyName("item")]
        public ChangeItem Item { get; set; }

        [JsonPropertyName("changeType")]
        public string ChangeType { get; set; }
    }

    public class ChangeItem
    {
        [JsonPropertyName("path")]
        public string Path { get; set; }

        [JsonPropertyName("objectId")]
        public string ObjectId { get; set; }
    }

    public class DiffResponse
    {
        [JsonPropertyName("changeCounts")]
        public ChangeCounts ChangeCounts { get; set; }
    }

    public class ChangeCounts
    {
        [JsonPropertyName("Add")]
        public int Add { get; set; }

        [JsonPropertyName("Delete")]
        public int Delete { get; set; }

        [JsonPropertyName("Edit")]
        public int Edit { get; set; }
    }

    public class PRCompleteDataWithDiffs : PRCompleteData
    {
        public Dictionary<string, FileDetailedDiff> FileDiffs { get; set; }

        public PRCompleteDataWithDiffs()
        {
            FileDiffs = new Dictionary<string, FileDetailedDiff>();
        }
    }

    public class FileDetailedDiff
    {
        public string FilePath { get; set; }
        public List<DiffLine> DiffLines { get; set; }
        public int AddedLines { get; set; }
        public int DeletedLines { get; set; }
        public int TotalChanges { get; set; }
        public string Error { get; set; }

        public FileDetailedDiff()
        {
            DiffLines = new List<DiffLine>();
        }
    }

    public class DiffLine
    {
        public LineType LineType { get; set; }
        public string Content { get; set; }
        public int? OldLineNumber { get; set; }
        public int? NewLineNumber { get; set; }

        public string LinePrefix => LineType switch
        {
            LineType.Added => "+",
            LineType.Deleted => "-",
            _ => " "
        };
    }

    public enum LineType
    {
        Unchanged,
        Added,
        Deleted,
        Modified
    }

}
