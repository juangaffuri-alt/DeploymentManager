using System.ComponentModel.DataAnnotations;

namespace PasadorScript.Models.ColasFechas
{
    public class Entorno
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Nombre { get; set; } // "DESA", "QA", "PROD"

        [StringLength(200)]
        public string Descripcion { get; set; }

        [StringLength(20)]
        public string Color { get; set; } // warning, info, danger, success

        [StringLength(50)]
        public string Icono { get; set; } // "fas fa-laptop-code"

        public bool Activo { get; set; } = true;

        // Navegación
        public virtual ICollection<TurnoModificacion> Turnos { get; set; }
    }

    public class TurnoModificacion
    {
        public int Id { get; set; }

        [Required]
        public int EntornoId { get; set; }

        [Required]
        [StringLength(450)]
        public string UsuarioId { get; set; }

        [Required]
        [StringLength(100)]
        public string NombreUsuario { get; set; }


        [Required]
        public DateTime FechaSolicitud { get; set; }

        [Required]
        [Range(1, 480)]
        public int MinutosEstimados { get; set; }

        public DateTime? InicioModificacion { get; set; }

        public DateTime? FinEstimado { get; set; }

        [Required]
        public EstadoTurno Estado { get; set; }

        [StringLength(500)]
        public string Motivo { get; set; }

        // Relaciones
        public virtual Entorno Entorno { get; set; }
    }

    public enum EstadoTurno
    {
        [Display(Name = "En Cola")]
        EnCola = 1,

        [Display(Name = "En Proceso")]
        EnProceso = 2,

        [Display(Name = "Completado")]
        Completado = 3,

        [Display(Name = "Cancelado")]
        Cancelado = 4,

        [Display(Name = "Liberado Forzado")]
        Forzado = 5
    }
}
