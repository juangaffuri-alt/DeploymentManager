using System.Text.Json.Serialization;

namespace PasadorScript.DTO.AzureDev
{
    public class AgentPoolResponseAzure
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }
        [JsonPropertyName("value")]
        public ICollection<AgentPool>? Value { get; set; }
    }
    public class AgentPool
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public bool IsHosted { get; set; }
        public bool IsLegacy { get; set; }
        public List<Agent> Agents { get; set; } = new();
    }

    public class AgentResponseAzure
    {
        public int Count { get; set; }
        public ICollection<Agent> Value { get; set; }
    }

    public class Agent
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Status { get; set; }
        public bool Enabled { get; set; }
    }
}
