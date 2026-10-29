namespace PasadorScript.DTO.AzureDev
{

    public class Branch
    {
        public string BranchName { get; set; }
        public ICollection<string> Tags { get; set; } = new List<string>();

    }
}
