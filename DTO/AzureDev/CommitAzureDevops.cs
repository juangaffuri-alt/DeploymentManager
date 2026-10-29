namespace PasadorScript.DTO.AzureDev
{
    public class CommitAzureDevops
    {
        public string? CommitID { get; set; }
        public string? Stage { get; set; }
        public string? Branch { get; set; }
    }
        
    public class CommitHistory
    {
        public string? CommitId { get; set; }
        public string? Author { get; set; }
        public DateTime Date { get; set; }
        public string? Message { get; set; }
        public string ShortMessage => Message!.Length > 100 ? Message.Substring(0, 100) + "..." : Message;
    }

    // Models/AzureDevOpsResponse.cs
    public class AzureDevOpsResponse
    {
        public int Count { get; set; }
        public List<Commit> Value { get; set; } = new List<Commit>();
    }

    public class Commit
    {
        public string? CommitId { get; set; }
        public Author? Author { get; set; }
        public Committer? Committer { get; set; }
        public string? Comment { get; set; }
        public DateTime Date { get; set; }
    }

    public class Author
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public DateTime Date { get; set; }
    }

    public class Committer
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public DateTime Date { get; set; }
    }
}
