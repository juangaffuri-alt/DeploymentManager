using Microsoft.EntityFrameworkCore;
using PasadorScript.DbModels;
using PasadorScript.Models.ColasFechas;
using PasadorScript.Services.AdGroups;
using PasadorScript.ViewModels;

namespace PasadorScript.Services.TurnosFechas
{
    public class TurnoService : ITurnoService
    {
        private readonly NuevoP229Context _context;
        private readonly ILogger<TurnoService> _logger;
        private readonly IAdGroupService _adGroupService;
        private readonly IFechaEntornoService _fechaActualService;

        public TurnoService(NuevoP229Context context, ILogger<TurnoService> logger, IAdGroupService adGroupService, IFechaEntornoService fechaActualService)
        {
            _context = context;
            _logger = logger;
            _adGroupService = adGroupService;
            _fechaActualService = fechaActualService;
        }

        public async Task<List<EntornoViewModel>> ObtenerEntornosActivosAsync()
        {
            return await _context.Entornos
                .Where(e => e.Activo)
                .Select(e => new EntornoViewModel
                {
                    Id = e.Id,
                    Nombre = e.Nombre,
                    Descripcion = e.Descripcion,
                    Color = e.Color,
                    Icono = e.Icono
                })
                .ToListAsync();
        }

        public async Task<EstadoEntornoViewModel> ObtenerEstadoEntornoAsync(int entornoId)
        {
            var entorno = await _context.Entornos.FindAsync(entornoId);
            if (entorno == null)
                return null;

            // Obtener fecha actual desde la base de datos externa
            DateTime? fechaActual = await _fechaActualService.ObtenerFechaActualAsync(entorno.Nombre);

            var turnoEnProceso = await _context.TurnosModificacions
            .Where(t => t.EntornoId == entornoId && t.Estado == (int)EstadoTurno.EnProceso)
            .OrderBy(t => t.InicioModificacion)
            .Select(t => new TurnoActivoViewModel
            {
                Id = t.Id,
                Usuario = t.NombreUsuario,
                FechaSolicitada = t.FechaSolicitada,
                Inicio = t.InicioModificacion.Value,
                FinEstimado = t.FinEstimado.Value,
                MinutosRestantes = (int)((t.FinEstimado.Value - DateTime.Now).TotalMinutes),
                Motivo = t.Motivo,
                //Ejecutado = t.FechaEjecucion != null   // ← nuevo
            })
            .FirstOrDefaultAsync();

            var cola = await _context.TurnosModificacions
                .Where(t => t.EntornoId == entornoId && t.Estado == (int)EstadoTurno.EnCola)
                .OrderBy(t => t.FechaSolicitud)
                .Select(t => new TurnoColaViewModel
                {
                    Id = t.Id,
                    Usuario = t.NombreUsuario,
                    FechaSolicitada = t.FechaSolicitada,
                    MinutosEstimados = t.MinutosEstimados,
                    Motivo = t.Motivo,
                    FechaSolicitud = t.FechaSolicitud
                })
                .ToListAsync();

            // Calcular tiempos de espera
            int tiempoAcumulado = turnoEnProceso?.MinutosRestantes ?? 0;
            for (int i = 0; i < cola.Count; i++)
            {
                cola[i].TiempoEsperaEstimado = tiempoAcumulado;
                cola[i].Posicion = i + 1;
                tiempoAcumulado += cola[i].MinutosEstimados;
            }

            return new EstadoEntornoViewModel
            {
                EntornoId = entornoId,
                EntornoNombre = entorno.Nombre,
                EntornoColor = entorno.Color,
                EntornoIcono = entorno.Icono,
                FechaActual = fechaActual,
                TurnoActivo = turnoEnProceso,
                Cola = cola,
                TotalEnCola = cola.Count
            };
        }

        public async Task<Dictionary<int, EstadoEntornoViewModel>> ObtenerEstadoTodosEntornosAsync()
        {
            var entornos = await ObtenerEntornosActivosAsync();
            var resultado = new Dictionary<int, EstadoEntornoViewModel>();

            foreach (var entorno in entornos)
            {
                resultado[entorno.Id] = await ObtenerEstadoEntornoAsync(entorno.Id);
            }

            return resultado;
        }

        public async Task<SolicitudResultViewModel> SolicitarTurnoAsync(
            string usuarioId,
            int entornoId,
            DateTime fechaSolicitada,
            int minutosParaEjecucion,
            int minutosDuracion,
            string motivo = null)
        {
            try
            {
                // Verificar turnos activos del usuario
                bool turnosActivos = await _context.TurnosModificacions
                    .AnyAsync(t => t.UsuarioId == usuarioId &&
                                  (t.Estado == (int)EstadoTurno.EnCola || t.Estado == (int)EstadoTurno.EnProceso));

                if (turnosActivos)
                {
                    return new SolicitudResultViewModel
                    {
                        Exitoso = false,
                        Mensaje = "Ya tienes un turno activo. Completa o cancela ese primero."
                    };
                }

                // Verificar si ya hay solicitud para esa fecha en ese entorno (opcional)
                bool existeSolicitud = await _context.TurnosModificacions
                    .AnyAsync(t => t.EntornoId == entornoId &&
                                  t.FechaSolicitada.Date == fechaSolicitada.Date &&
                                  t.Estado == (int)EstadoTurno.EnCola);

                if (existeSolicitud)
                {
                    return new SolicitudResultViewModel
                    {
                        Exitoso = false,
                        Mensaje = "Ya hay una solicitud pendiente para esa fecha"
                    };
                }

                string nombreUsuario = await ObtenerNombreUsuario(usuarioId);

                var turno = new TurnosModificacion
                {
                    EntornoId = entornoId,
                    UsuarioId = usuarioId,
                    NombreUsuario = nombreUsuario,
                    FechaSolicitada = fechaSolicitada.Date,
                    FechaSolicitud = DateTime.Now,
                    MinutosParaEjecucion = minutosParaEjecucion,
                    MinutosEstimados = minutosDuracion,
                    Estado = (int)EstadoTurno.EnCola,
                    Motivo = motivo
                };

                _context.TurnosModificacions.Add(turno);
                await _context.SaveChangesAsync();

                int posicion = await _context.TurnosModificacions
                    .CountAsync(t => t.EntornoId == entornoId &&
                                   t.Estado == (int)EstadoTurno.EnCola &&
                                   t.FechaSolicitud < turno.FechaSolicitud) + 1;

                var entorno = await _context.Entornos.FindAsync(entornoId);

                return new SolicitudResultViewModel
                {
                    Exitoso = true,
                    TurnoId = turno.Id,
                    PosicionCola = posicion,
                    Mensaje = $"Solicitud recibida para {entorno?.Nombre} - Fecha: {fechaSolicitada:dd/MM/yyyy}. Posición: {posicion}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al solicitar turno");
                return new SolicitudResultViewModel
                {
                    Exitoso = false,
                    Mensaje = "Error al procesar la solicitud"
                };
            }
        }

        public async Task<bool> IniciarTurnoAsync(int turnoId, string usuarioId)
        {
            var turno = await _context.TurnosModificacions
                .FirstOrDefaultAsync(t => t.Id == turnoId && t.UsuarioId == usuarioId);

            if (turno == null || turno.Estado != (int)EstadoTurno.EnCola)
                return false;

            // Verificar que sea el primero en la cola
            var primeroEnCola = await _context.TurnosModificacions
                .Where(t => t.EntornoId == turno.EntornoId && t.Estado == (int)EstadoTurno.EnCola)
                .OrderBy(t => t.FechaSolicitud)
                .FirstOrDefaultAsync();

            if (primeroEnCola?.Id != turno.Id)
                return false;

            var ahora = DateTime.Now;
            turno.Estado = (int)EstadoTurno.EnProceso;
            turno.InicioModificacion = ahora;
            turno.InicioEjecucion = ahora.AddMinutes(turno.MinutosParaEjecucion);
            turno.FinEstimado = turno.InicioEjecucion.Value.AddMinutes(turno.MinutosEstimados);

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> LiberarTurnoAsync(int turnoId, string usuarioId, bool forzado = false)
        {
            var turno = await _context.TurnosModificacions
                .FirstOrDefaultAsync(t => t.Id == turnoId);

            if (turno == null)
                return false;

            if (turno.UsuarioId != usuarioId && !forzado)
                return false;

            turno.Estado = forzado ? (int)EstadoTurno.Forzado : (int)EstadoTurno.Completado;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<MiTurnoViewModel>> ObtenerTurnosUsuarioAsync(string usuarioId)
        {
            var ahora = DateTime.Now;

            var turnos = await _context.TurnosModificacions
                .Include(t => t.Entorno)
                .Where(t => t.UsuarioId == usuarioId &&
                           (t.Estado == (int)EstadoTurno.EnCola || t.Estado == (int)EstadoTurno.EnProceso))
                .Select(t => new MiTurnoViewModel
                {
                    Id = t.Id,
                    EntornoId = t.EntornoId,
                    EntornoNombre = t.Entorno != null ? t.Entorno.Nombre : "Sin entorno",
                    EntornoColor = t.Entorno != null ? t.Entorno.Color : "secondary",
                    Estado = ((EstadoTurno)t.Estado).ToString(),   // ← Conversión correcta
                    FechaSolicitada = t.FechaSolicitada,
                    MinutosParaEjecucion = t.MinutosParaEjecucion,
                    MinutosDuracion = t.MinutosEstimados,
                    FechaSolicitud = t.FechaSolicitud,
                    InicioModificacion = t.InicioModificacion,
                    InicioEjecucion = t.InicioEjecucion,
                    FinEstimado = t.FinEstimado,
                    FechaEjecucion = t.FechaEjecucion,
                    Motivo = t.Motivo,
                    PosicionCola = 0,
                    TiempoEspera = 0
                })
                .ToListAsync();

            // Calcular posición y espera para los que están en cola
            foreach (var turno in turnos.Where(t => t.Estado == "EnCola"))
            {
                turno.PosicionCola = await _context.TurnosModificacions
                    .CountAsync(t => t.EntornoId == turno.EntornoId &&
                                   t.Estado == (int)EstadoTurno.EnCola &&
                                   t.FechaSolicitud < turno.FechaSolicitud) + 1;

                var turnoEnProceso = await _context.TurnosModificacions
                    .FirstOrDefaultAsync(t => t.EntornoId == turno.EntornoId &&
                                            t.Estado == (int)EstadoTurno.EnProceso);

                int tiempoAcumulado = turnoEnProceso != null
                    ? (int)((turnoEnProceso.FinEstimado - ahora).Value.TotalMinutes)
                    : 0;

                var turnosPrevios = await _context.TurnosModificacions
                    .Where(t => t.EntornoId == turno.EntornoId &&
                               t.Estado == (int)EstadoTurno.EnCola &&
                               t.FechaSolicitud < turno.FechaSolicitud)
                    .ToListAsync();

                tiempoAcumulado += turnosPrevios.Sum(t => t.MinutosEstimados);
                turno.TiempoEspera = Math.Max(0, tiempoAcumulado);
            }

            return turnos;
        }

        public async Task<MiTurnoDetalleViewModel> ObtenerDetalleTurnoAsync(int turnoId, string usuarioId)
        {
            var turno = await _context.TurnosModificacions
                .Include(t => t.Entorno)
                .FirstOrDefaultAsync(t => t.Id == turnoId && t.UsuarioId == usuarioId);

            if (turno == null)
                return null;

            // Obtener fecha actual del entorno
            DateTime? fechaActual = await _fechaActualService.ObtenerFechaActualAsync(turno.Entorno.Nombre);

            var vm = new MiTurnoDetalleViewModel
            {
                Id = turno.Id,
                EntornoId = turno.EntornoId,
                EntornoNombre = turno.Entorno.Nombre,
                Estado = ((EstadoTurno)turno.Estado).ToString(),
                FechaSolicitada = turno.FechaSolicitada,
                FechaSolicitud = turno.FechaSolicitud,
                Motivo = turno.Motivo,
                FechaActual = fechaActual,   // ← NUEVO
                MinutosParaEjecucion = turno.MinutosParaEjecucion,
                MinutosDuracion = turno.MinutosEstimados,
                InicioEjecucion = turno.InicioEjecucion,
                FechaEjecucion = turno.FechaEjecucion,
                FinEstimado = turno.FinEstimado,
                InicioModificacion = turno.InicioModificacion
            };

            // Cálculos para cola...
            if (turno.Estado == (int)EstadoTurno.EnCola)
            {
                vm.PosicionCola = await _context.TurnosModificacions
                    .CountAsync(t => t.EntornoId == turno.EntornoId &&
                                   t.Estado == (int)EstadoTurno.EnCola &&
                                   t.FechaSolicitud < turno.FechaSolicitud) + 1;

                var turnoEnProceso = await _context.TurnosModificacions
                    .FirstOrDefaultAsync(t => t.EntornoId == turno.EntornoId &&
                                            t.Estado == (int)EstadoTurno.EnProceso);

                int tiempoAcumulado = turnoEnProceso != null
                    ? (int)((turnoEnProceso.FinEstimado - DateTime.Now).Value.TotalMinutes)
                    : 0;

                var turnosPrevios = await _context.TurnosModificacions
                    .Where(t => t.EntornoId == turno.EntornoId &&
                               t.Estado == (int)EstadoTurno.EnCola &&
                               t.FechaSolicitud < turno.FechaSolicitud)
                    .ToListAsync();

                tiempoAcumulado += turnosPrevios.Sum(t => t.MinutosEstimados);
                vm.TiempoEspera = Math.Max(0, tiempoAcumulado);

                int tiempoTotal = vm.TiempoEspera + turno.MinutosEstimados;
                vm.ProgresoEspera = tiempoTotal > 0 ? (double)vm.TiempoEspera / tiempoTotal * 100 : 0;
            }

            return vm;
        }

        public async Task<bool> CancelarTurnoAsync(int turnoId, string usuarioId)
        {
            var turno = await _context.TurnosModificacions
                .FirstOrDefaultAsync(t => t.Id == turnoId && t.UsuarioId == usuarioId);

            if (turno == null || turno.Estado != (int)EstadoTurno.EnCola)
                return false;

            turno.Estado = (int)EstadoTurno.Cancelado;
            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<string> ObtenerNombreUsuario(string usuarioId)
        {
            var user = await _adGroupService.GetInfoUser();
            return user?.Username ?? usuarioId;
        }
    }
}