using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PasadorScript.Services.TurnosFechas;
using PasadorScript.ViewModels;

namespace PasadorScript.Controllers
{
    [Authorize]
    public class TurnoFechasController : Controller
    {
        private readonly ITurnoService _turnoService;
        private readonly ILogger<TurnoFechasController> _logger;

        public TurnoFechasController(ITurnoService turnoService, ILogger<TurnoFechasController> logger)
        {
            _turnoService = turnoService;
            _logger = logger;
        }

        // GET: Turno
        public async Task<IActionResult> Index()
        {
            try
            {
                var entornos = await _turnoService.ObtenerEntornosActivosAsync();
                var estados = await _turnoService.ObtenerEstadoTodosEntornosAsync();
                var misTurnos = await _turnoService.ObtenerTurnosUsuarioAsync(GetUserId());

                var viewModel = new DashboardViewModel
                {
                    Entornos = entornos,
                    EstadosPorEntorno = estados,
                    MisTurnos = misTurnos
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar dashboard");
                TempData["Error"] = "Error al cargar el dashboard";
                return View(new DashboardViewModel());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Solicitar(SolicitudTurnoViewModel model)
        {
            try
            {
                // Verificar si es petición AJAX
                bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

                if (!ModelState.IsValid)
                {
                    if (isAjax)
                    {
                        var errors = string.Join(" | ", ModelState.Values
                            .SelectMany(v => v.Errors)
                            .Select(e => e.ErrorMessage));
                        return Json(new { success = false, message = "Datos inválidos: " + errors });
                    }
                    TempData["Error"] = "Datos inválidos";
                    return RedirectToAction(nameof(Index));
                }

                var resultado = await _turnoService.SolicitarTurnoAsync(
                    GetUserId(),
                    model.EntornoId,
                    model.FechaSolicitada,
                    model.MinutosParaEjecucion, 
                    model.MinutosDuracion,
                    model.Motivo);

                if (isAjax)
                {
                    return Json(new
                    {
                        success = resultado.Exitoso,
                        message = resultado.Mensaje,
                        turnoId = resultado.TurnoId
                    });
                }

                if (resultado.Exitoso)
                {
                    TempData["Success"] = resultado.Mensaje;
                    return RedirectToAction(nameof(MiTurno), new { id = resultado.TurnoId });
                }

                TempData["Error"] = resultado.Mensaje;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                // Loggear el error (inyecta ILogger en el constructor)
                _logger.LogError(ex, "Error en Solicitar");

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Error interno del servidor. Intente nuevamente." });
                }

                TempData["Error"] = "Error interno";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Turno/MiTurno/5
        public async Task<IActionResult> MiTurno(int id)
        {
            var turno = await _turnoService.ObtenerDetalleTurnoAsync(id, GetUserId());

            if (turno == null)
            {
                return NotFound();
            }

            return View(turno);
        }

        // POST: Turno/Iniciar/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Iniciar(int id)
        {
            var iniciado = await _turnoService.IniciarTurnoAsync(id, GetUserId());

            if (iniciado)
            {
                TempData["Success"] = "Turno iniciado. Tienes el control del entorno.";
                return RedirectToAction(nameof(Modificando), new { id });
            }

            TempData["Error"] = "No se pudo iniciar el turno";
            return RedirectToAction(nameof(MiTurno), new { id });
        }

        // GET: Turno/Modificando/5
        public async Task<IActionResult> Modificando(int id)
        {
            var turno = await _turnoService.ObtenerDetalleTurnoAsync(id, GetUserId());

            if (turno == null || !turno.EsMiTurno)
            {
                return NotFound();
            }

            return View(turno);
        }

        // POST: Turno/Liberar/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Liberar(int id, bool forzado = false)
        {
            var liberado = await _turnoService.LiberarTurnoAsync(id, GetUserId(), forzado);

            if (liberado)
            {
                TempData["Success"] = "Turno liberado correctamente";
            }
            else
            {
                TempData["Error"] = "No se pudo liberar el turno";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Turno/Cancelar/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancelar(int id)
        {
            var cancelado = await _turnoService.CancelarTurnoAsync(id, GetUserId());

            if (cancelado)
            {
                TempData["Success"] = "Turno cancelado correctamente";
            }
            else
            {
                TempData["Error"] = "No se pudo cancelar el turno";
            }

            return RedirectToAction(nameof(Index));
        }

        private string GetUserId()
        {
            return User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        }
    }
}
