using PasadorScript.DTO.AzureDev;

namespace PasadorScript.ViewModels
{
    public enum TipoCambio { 
        Scripts = 1,
        Genexus = 2,
        EnviosDlya = 3
    }
    public class AzureDevopsPullRequestExtend: AzureDevopsPullRequest
    {
        public int IdType { get; set; }
        public TipoCambio Type { get; set; }
        public string TypeDescription => Type switch
        {
            TipoCambio.Scripts => "Script SQL",
            TipoCambio.Genexus => "Build GX",
            TipoCambio.EnviosDlya => "Envío Dlya",
            _ => "Desconocido"
        };
        public int NroIncidente { get; set; }
        public string DescripcionIncidente { get; set; } = "";
        public string Descripcion { get; set; } = "";
    }
}
