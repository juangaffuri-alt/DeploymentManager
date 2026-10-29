using Microsoft.EntityFrameworkCore;
using PasadorScript.DbModels;
using PasadorScript.Models.ColasFechas;

namespace PasadorScript.Services.TurnosFechas
{
    public class ControlTurnosBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ControlTurnosBackgroundService> _logger;

        public ControlTurnosBackgroundService(IServiceProvider serviceProvider, ILogger<ControlTurnosBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<NuevoP229Context>();
                    var ahora = DateTime.Now;

                    // 1. Ejecutar cambios de fecha (cuando InicioEjecucion <= ahora y aún no ejecutado)
                    var turnosAEjecutar = await context.TurnosModificacions
                        .Include(t => t.Entorno)
                        .Where(t => t.Estado == (int)EstadoTurno.EnProceso &&
                                    t.InicioEjecucion <= ahora &&
                                    t.FechaEjecucion == null)
                        .ToListAsync();

                    foreach (var turno in turnosAEjecutar)
                    {
                        try
                        {
                            _logger.LogInformation($"Ejecutando cambio de fecha en {turno.Entorno.Nombre} a {turno.FechaSolicitada:dd/MM/yyyy}");
                            // Aquí llamada al sistema externo
                            await EjecutarCambioFechaExterno(turno);

                            turno.FechaEjecucion = ahora;
                            _logger.LogInformation($"Cambio ejecutado para turno {turno.Id}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error ejecutando cambio para turno {turno.Id}");
                        }
                    }

                    // 2. Completar turnos que ya terminaron su duración (FinEstimado <= ahora)
                    var turnosACompletar = await context.TurnosModificacions
                        .Where(t => t.Estado == (int)EstadoTurno.EnProceso &&
                                    t.FinEstimado <= ahora)
                        .ToListAsync();

                    foreach (var turno in turnosACompletar)
                    {
                        turno.Estado = (int)EstadoTurno.Completado;
                        _logger.LogInformation($"Turno {turno.Id} completado");
                    }

                    // 3. Avanzar cola: pasar el siguiente de la cola a EnProceso
                    var entornos = await context.Entornos.Where(e => e.Activo).ToListAsync();
                    foreach (var entorno in entornos)
                    {
                        bool hayActivo = await context.TurnosModificacions
                            .AnyAsync(t => t.EntornoId == entorno.Id && t.Estado == (int)EstadoTurno.EnProceso);

                        if (!hayActivo)
                        {
                            var siguiente = await context.TurnosModificacions
                                .Where(t => t.EntornoId == entorno.Id && t.Estado == (int)EstadoTurno.EnCola)
                                .OrderBy(t => t.FechaSolicitud)
                                .FirstOrDefaultAsync();

                            if (siguiente != null)
                            {
                                var ahoraLocal = DateTime.Now;
                                siguiente.Estado = (int)EstadoTurno.EnProceso;
                                siguiente.InicioModificacion = ahoraLocal;
                                siguiente.InicioEjecucion = ahoraLocal.AddMinutes(siguiente.MinutosParaEjecucion);
                                siguiente.FinEstimado = siguiente.InicioEjecucion.Value.AddMinutes(siguiente.MinutosEstimados);
                                _logger.LogInformation($"Turno {siguiente.Id} iniciado para entorno {entorno.Nombre}");
                            }
                        }
                    }

                    if (turnosAEjecutar.Any() || turnosACompletar.Any() || entornos.Any())
                        await context.SaveChangesAsync();
                }

                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        private Task EjecutarCambioFechaExterno(TurnosModificacion turno)
        {
            // Implementar llamada real a la base de datos externa si es necesario
            return Task.CompletedTask;
        }
    }
}