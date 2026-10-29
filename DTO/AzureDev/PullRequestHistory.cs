
namespace PasadorScript.DTO.AzureDev
{
    public abstract class VoteBase
    {
        public int Vote { get; set; }

        public string VoteLabel => Vote switch
        {
            10 => "Aprobado",
            5 => "Aprobado con sugerencias",
            0 => "No Voto",
            -5 => "Esperando autor",
            -10 => "Rechazado",
            _ => "Unknown"
        };
        public string BadgeClass => Vote switch
        {
            10 => "badge badge-success",
            5 => "badge badge-warning",
            0 => "badge badge-black",
            -5 => "badge badge-black",
            -10 => "badge badge-danger",
            _ => "badge badge-info"
        };
    }

    public class VoteEvent:VoteBase
    {
        public string User { get; set; }
        public string UrlImage { get; set; }
        public DateTime Timestamp { get; set; }
        public string Content { get; set; }
        public bool IsVote { get; set; }
        public string IconType => IsVote switch
        {
            true => "icon-tag",
            false => "icon-bubble"
        };

    }
    public class ReviewerInfo:VoteBase
    {
        public string Name { get; set; }
        public string UrlImage { get; set; }
        
    }
    public class PullRequestHistory
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Status { get; set; }
        public string Author { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<ReviewerInfo> Reviewers { get; set; } = new();
        public List<VoteEvent> VoteHistory { get; set; } = new();
        public string StatusLabel => Status switch
        {
            "active" => "Activo",
            "completed" => "Aplicado",
            "abandoned" => "Abandonado"
        };
    }
}
