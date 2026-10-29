namespace PasadorScript.DTO.AzureDev
{
    // Modelo para las estructuras SQL
    public class SqlStructure
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Path { get; set; }
        public string Schema { get; set; } = "dbo";
        public DateTime LastModified { get; set; }
        public string Branch { get; set; } = "main";
        public long Size { get; set; }
        public string Description { get; set; }
    }

    // DTOs para los requests del controller
    public class SearchStructuresRequest
    {
        public string RepositoryId { get; set; }
        public string StructureType { get; set; }
        public string SearchTerm { get; set; }
    }

    public class GetStructureContentRequest
    {
        public string RepositoryId { get; set; }
        public string FilePath { get; set; }
    }

    public class GetContentForEditRequest
    {
        public string Content { get; set; } = "";
        public string Type { get; set; } = "";
        public string Schema { get; set; } = "";
        public string Name { get; set; } = "";
    }

    public class StructureSQLType
    {
        public string Type { get; set; } = "";
        public bool Seleccionable { get; set; }
        public string Title { get; set; } = "";
        public string Script { get; set; } = "";
        public string Description { get; set; } = "";
        public string IClass { get; set; } = "";
        public string StructureName { get; set; } = "";

    }

    public class StructureSQLContentTemplate
    {
        public string Content { get; set; } = "";
        public List<int> RequiredLines { get; set; } = new();

    }

    public class PRReleaseStatus
    {
        public bool HasBuild { get; set; }
        public bool HasRelease { get; set; }
        public int? BuildId { get; set; }
        public int? ReleaseId { get; set; }
        public string? ReleaseStatus { get; set; }
        public string? ReleaseName { get; set; }
        public string Message { get; set; } = "";
    }
    public class PullRequestInfo
    {
        public int Id { get; set; }
        public string Status { get; set; } = ""; // "active", "completed", "abandoned"
        public string TargetBranch { get; set; } = "";
        public string SourceBranch { get; set; } = "";
        public CommitInfo? LastMergeCommit { get; set; }
    }

    

    // ------- Clases minimalistas para deserializar ------------
    record BuildDefList(List<BuildDef> Value = null!);
    record BuildDef(int Id, string Name, ProjectRef Project, RepositoryInfo Repository);
    record ProjectRef(string Id);

    record ReleaseDefList(List<ReleaseDef> Value = null!);
    record ReleaseDef(int Id, string Name);

    record ProjectList(List<ProjectInfo> Value = null!);
    record ProjectInfo(string Id, string Name);



}
