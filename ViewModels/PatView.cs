using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace PasadorScript.ViewModels
{
    public class PatView
    {
        [Required]
        [DisplayName("Personal Access Token")]
        [MinLength(10)]
        public  string Pat { get; set; }
    }
}
