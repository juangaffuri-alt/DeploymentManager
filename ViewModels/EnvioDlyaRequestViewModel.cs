using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace PasadorScript.ViewModels
{
    public class EnvioDlyaRequestViewModel
    {
        public int? IdEnvioDlya { get; set; }

        [Display(Name = "Descripción")]
        [Required(ErrorMessage = "Campo obligatorio")]
        [MaxLength(255, ErrorMessage = "Ingreso máximo de 255 caracteres")]
        public string Descripcion { get; set; }

        [Required(ErrorMessage = "Seleccioná un valor de la lista")]
        [DisplayName("Aplicativo")]
        public string AzureProjectType { get; set; } = "Genexus";
        public string RepositoryId { get; set; }
        [Required]
        [DisplayName("Número")]
        [Range(1, int.MaxValue, ErrorMessage = "El valor debe ser mayor o igual a 1")]
        public int Numero { get; set; } = 0;
        [Required(AllowEmptyStrings = false, ErrorMessage = "Seleccioná un valor de la lista")]
        [DisplayName("Clasificación")]
        public int Tipo { get; set; } = 1;
        /// <summary>
        /// Nombre único para crear folder en el repo y branch en desarrollo
        /// </summary>
        public string? FolderAndBranchNameDesa { get; set; }
        [Required(ErrorMessage = "Agregá al menos un archivo o agregalo desde un template")]
        [DisplayName("Archivo/s o nuevo contenido desde template")]
        public string? Usuario { get; set; }
        public int? PullRequestId { get; set; }
        public string JsonVirtualFiles { get; set; } = "";
        public string? JsonZipData { get; set; }
        public ICollection<FileView> Files { get; set; } = new List<FileView>();
        public ICollection<VirtualFileViewModel> FilesRequest { get; set; } = new List<VirtualFileViewModel>();
        public string TimeStampJson { get; set; } = "";
        public bool IsHotfix { get; set; }
        public string? TareasJson { get; set; }
    }

    public class ZipData
    {
        public string Base64 { get; set; }
        public List<ArchivoInfo> Archivos { get; set; }
        public int TotalArchivos { get; set; }
        public DateTime FechaGeneracion { get; set; }
    }

    public class ArchivoInfo
    {
        public string Nombre { get; set; }
        public string RutaOriginal { get; set; }
        public long Tamaño { get; set; }
        public bool EsBinario { get; set; }
        public DateTime Fecha { get; set; }
    }
}
