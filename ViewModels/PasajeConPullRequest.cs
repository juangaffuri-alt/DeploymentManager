using PasadorScript.DbModels;

namespace PasadorScript.ViewModels
{
    public class GenericPrHist
    {
        public int Id { get; set; }
        public int IdPr { get; set; }
    }
    public class PasajeConPullRequest
    {
        public bool EsGx { get; set; }
        public string AzureProjectType { get; set; } = "Default";
        public string RepositoryId { get; set; }
        public int IdScript { get; set; }
        public string DescripcionTipoPasaje { get; set; }
        public int NumeroIncidente { get; set; }
        public string DescripcionScript { get; set; }
        public int? PullRequestId { get; set; }
        public string BranchNamePR { get; set; }
        public ICollection<GenericPrHist> PullRequestHistoricos { get; set; } = new List<GenericPrHist>();
    }
}
