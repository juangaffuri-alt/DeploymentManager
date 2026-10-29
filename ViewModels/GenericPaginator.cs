using X.PagedList;

namespace PasadorScript.ViewModels
{
    public class GenericPaginator<T> where T : class
    {
        public string Search { get; set; }
        public int PaginaActual { get; set; }
        public int RegistrosPorPagina { get; set; }
        public int TotalRegistros { get; set; }
        public int TotalPaginas { get; set; }
        public IPagedList<T> Resultado { get; set; }
    }
}
