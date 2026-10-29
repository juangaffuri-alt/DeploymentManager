using PasadorScript.ViewModels;

namespace PasadorScript.DTO.TeamDev
{
    public enum SearchTermType
    {
        Genexus = 1,
        Script = 2,
        Dlya = 3,
    }
    public class SearchTerm
    {
        public SearchTermType Type { get; set; }
        public string IconType => Type switch
        {
            SearchTermType.Genexus => "fas fa-rocket",
            SearchTermType.Script => "fas fa-file-code",
            SearchTermType.Dlya => "fas fa-box-open",
            _ => "Desconocido",
        };
        public string DescriptionType => Type switch
        {
            SearchTermType.Genexus => "Build GX",
            SearchTermType.Script => "Script",
            SearchTermType.Dlya => "Dlya",
            _ => "Desconocido",
        };
        public int Id { get; set; }
        public string Description { get; set; }
        public string Title { get; set; }
        public int PullRequestId { get; set; }
        public string Autor { get; set; }
        public int TipoCambio { get; set; }
        public string DescripcionTipoCambio { get; set; }
        public int NroIncidente { get; set; }
        public DateTime Date { get; set; }
        public string AzureProjectType { get; set; }
    }
    public class SearchAll: GenericPaginator<SearchTerm>
    {
        public string Expression { get; set; }
    }
}
