using PasadorScript.ViewModels;

namespace PasadorScript.Services.TurnosFechas
{
    public interface ITurnoService
    {
        Task<List<EntornoViewModel>> ObtenerEntornosActivosAsync();
        Task<EstadoEntornoViewModel> ObtenerEstadoEntornoAsync(int entornoId);
        Task<Dictionary<int, EstadoEntornoViewModel>> ObtenerEstadoTodosEntornosAsync();
        Task<SolicitudResultViewModel> SolicitarTurnoAsync(string usuarioId, int entornoId, DateTime fechaSolicitada, int minutosParaEjecucion, int minutosDuracion, string motivo);
        Task<bool> IniciarTurnoAsync(int turnoId, string usuarioId);
        Task<bool> LiberarTurnoAsync(int turnoId, string usuarioId, bool forzado = false);
        Task<List<MiTurnoViewModel>> ObtenerTurnosUsuarioAsync(string usuarioId);
        Task<MiTurnoDetalleViewModel> ObtenerDetalleTurnoAsync(int turnoId, string usuarioId);
        Task<bool> CancelarTurnoAsync(int turnoId, string usuarioId);
    }
}