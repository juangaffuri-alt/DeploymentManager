using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace PasadorScript.ViewModels
{
    public class VirtualFileInput
    {
        public string FileName { get; set; }
        public string Content { get; set; }
    }
    public class VirtualFileModel
    {
        public string FileName { get; set; }
        public string Content { get; set; }
    }

    public class SaveVirtualFilesRequest
    {
        public List<VirtualFileModel> Files { get; set; }
    }
    public class Pasaje
    {
        [Display(Name = "Descripción")]
        [Required(ErrorMessage = "Campo obligatorio")]
        [MaxLength(255, ErrorMessage = "Ingreso máximo de 255 caracteres")]
        public string Descripcion { get; set; }
        
        [Required(AllowEmptyStrings = false, ErrorMessage = "Seleccione un valor de la lista")]
        [DisplayName("Aplicativo")]
        
        public string UniqueIdApp { get; set; }
        [Required]
        [DisplayName("Número")]
        [Range(1,int.MaxValue, ErrorMessage = "El valor debe ser mayor o igual a 1")]
        public int Numero { get; set; }
        [Required(AllowEmptyStrings = false, ErrorMessage = "Seleccione un valor de la lista")]
        [DisplayName("Clasificación")]
        public int Tipo { get; set; }
        public int? PullRequestId { get; set; }
        public string? UniqueIdPasaje { get; set; }
        public string? CommitId { get; set; }
        [Required]
        [DisplayName("Opciones de guardado")]
        public int GuardarYLiberar { get; set; }
        public List<VirtualFileInput> VirtualFiles { get; set; } = new();
        public List<VirtualFileInput> UploadedFiles { get; set; } = new();

    }
}
