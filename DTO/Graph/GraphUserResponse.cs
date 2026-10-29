namespace PasadorScript.DTO.Graph
{
    public class GraphUserResponse
    {
        public string Descriptor { get; set; }
        public string Id { get; set; }
    }

    public class Reviewer
    {
        public string Id { get; set; }
        public int Vote { get; set; }
    }

    public class ReviewersListResponse
    {
        public List<Reviewer> Value { get; set; }
    }
}
