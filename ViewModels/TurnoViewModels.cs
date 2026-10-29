using System.ComponentModel.DataAnnotations;

namespace PasadorScript.ViewModels
{
    public class DashboardViewModel
    {
        public List<EntornoViewModel> Entornos { get; set; }
        public Dictionary<int, EstadoEntornoViewModel> EstadosPorEntorno { get; set; }
        public List<MiTurnoViewModel> MisTurnos { get; set; }
    }

    public class EntornoViewModel
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public string Color { get; set; }
        public string Icono { get; set; }
    }

    public class EstadoEntornoViewModel
    {
        public int EntornoId { get; set; }
        public string EntornoNombre { get; set; }
        public string EntornoColor { get; set; }
        public string EntornoIcono { get; set; }
        public DateTime? FechaActual { get; set; }               // Fecha actual del entorno (externa)
        public TurnoActivoViewModel TurnoActivo { get; set; }
        public List<TurnoColaViewModel> Cola { get; set; }
        public int TotalEnCola { get; set; }
    }

    public class TurnoActivoViewModel
    {
        public int Id { get; set; }
        public string Usuario { get; set; }
        public DateTime FechaSolicitada { get; set; }
        public DateTime Inicio { get; set; }
        public DateTime FinEstimado { get; set; }
        public int MinutosRestantes { get; set; }                // Tiempo total restante (FinEstimado - ahora)
        public string Motivo { get; set; }
        public DateTime? FechaEjecucion { get; set; }            // Cuándo se ejecutó realmente
        public bool Ejecutado => FechaEjecucion.HasValue;
        // No tiene FechaActual porque eso pertenece al entorno, no al turno activo
    }

    public class TurnoColaViewModel
    {
        public int Id { get; set; }
        public string Usuario { get; set; }
        public DateTime FechaSolicitada { get; set; }
        public int MinutosEstimados { get; set; }                // duración Z
        public string Motivo { get; set; }
        public DateTime FechaSolicitud { get; set; }
        public int TiempoEsperaEstimado { get; set; }
        public int Posicion { get; set; }
        // No tiene FechaActual, se mostrará la del entorno (ya está en EstadoEntornoViewModel)
    }

    public class MiTurnoViewModel
    {
        public int Id { get; set; }
        public int EntornoId { get; set; }
        public string EntornoNombre { get; set; }
        public string EntornoColor { get; set; }
        public string Estado { get; set; }            // "EnCola", "EnProceso", etc.
        public DateTime FechaSolicitada { get; set; }
        public int MinutosParaEjecucion { get; set; } // Y
        public int MinutosDuracion { get; set; }      // Z
        public DateTime FechaSolicitud { get; set; }
        public DateTime? InicioModificacion { get; set; }
        public DateTime? InicioEjecucion { get; set; }
        public DateTime? FinEstimado { get; set; }
        public DateTime? FechaEjecucion { get; set; }
        public int PosicionCola { get; set; }
        public int TiempoEspera { get; set; }
        public string Motivo { get; set; }

        public bool EsMiTurno => Estado == "EnProceso";
        public bool Ejecutado => FechaEjecucion.HasValue;

        public int? MinutosRestantes
        {
            get
            {
                if (FinEstimado.HasValue && Estado == "EnProceso")
                {
                    var restantes = (int)(FinEstimado.Value - DateTime.Now).TotalMinutes;
                    return Math.Max(0, restantes);
                }
                return null;
            }
        }

        public int? MinutosParaEjecucionRestantes
        {
            get
            {
                if (InicioEjecucion.HasValue && Estado == "EnProceso" && !Ejecutado)
                {
                    var restantes = (int)(InicioEjecucion.Value - DateTime.Now).TotalMinutes;
                    return Math.Max(0, restantes);
                }
                return null;
            }
        }
    }

    public class MiTurnoDetalleViewModel
    {
        public int Id { get; set; }
        public int EntornoId { get; set; }
        public string EntornoNombre { get; set; }
        public string Estado { get; set; }
        public DateTime FechaSolicitada { get; set; }
        public DateTime FechaSolicitud { get; set; }
        public string Motivo { get; set; }
        public DateTime? FechaActual { get; set; }                 // Fecha actual del entorno (externa)

        public int MinutosParaEjecucion { get; set; }
        public int MinutosDuracion { get; set; }
        public DateTime? InicioEjecucion { get; set; }
        public DateTime? FechaEjecucion { get; set; }
        public DateTime? FinEstimado { get; set; }

        public bool EsMiTurno => Estado == "EnProceso";
        public bool Ejecutado => FechaEjecucion.HasValue;

        public int? MinutosParaEjecucionRestantes
        {
            get
            {
                if (!Ejecutado && InicioEjecucion.HasValue && EsMiTurno)
                {
                    var restantes = (int)(InicioEjecucion.Value - DateTime.Now).TotalMinutes;
                    return Math.Max(0, restantes);
                }
                return null;
            }
        }

        public int? MinutosRestantes
        {
            get
            {
                if (Ejecutado && FinEstimado.HasValue && EsMiTurno)
                {
                    var restantes = (int)(FinEstimado.Value - DateTime.Now).TotalMinutes;
                    return Math.Max(0, restantes);
                }
                return null;
            }
        }

        public int PosicionCola { get; set; }
        public int TiempoEspera { get; set; }
        public double ProgresoEspera { get; set; }
        public DateTime? InicioModificacion { get; set; }
    }

    public class SolicitudTurnoViewModel
    {
        [Required]
        public int EntornoId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime FechaSolicitada { get; set; }

        [Required]
        [Range(1, 480)]
        [Display(Name = "Minutos hasta ejecución (Y)")]
        public int MinutosParaEjecucion { get; set; }

        [Required]
        [Range(1, 480)]
        [Display(Name = "Minutos de duración (Z)")]
        public int MinutosDuracion { get; set; }

        [StringLength(500)]
        public string Motivo { get; set; }
    }

    public class SolicitudResultViewModel
    {
        public bool Exitoso { get; set; }
        public int TurnoId { get; set; }
        public int PosicionCola { get; set; }
        public string Mensaje { get; set; }
    }
}