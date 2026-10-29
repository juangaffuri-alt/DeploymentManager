using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;

namespace PasadorScript.ViewModels
{
    public class ScriptRequestViewModel
    {
        public int? IdScript { get; set; }

        [Display(Name = "Descripción")]
        [Required(ErrorMessage = "Campo obligatorio")]
        [MaxLength(255, ErrorMessage = "Ingreso máximo de 255 caracteres")]
        public string Descripcion { get; set; }

        [Required(ErrorMessage = "Seleccioná un valor de la lista")]
        [DisplayName("Aplicativo")]
        public string RepositoryId { get; set; }
        public string AzureProjectType { get; set; } = "Default";
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
        public string JsonVirtualFiles { get; set; } = "";
        public string? CommitId { get; set; }
        public string? Usuario { get; set; }
        public int? PullRequestIdQA { get; set; }
        public int? PullRequestIdPR { get; set; }

        public ICollection<FileView> Files { get; set; } = new List<FileView>();
        public bool GeneratePR { get; set; }
        public bool IsHotfix { get; set; }
        public string? TareasJson { get; set; }
    }
    
    
}
