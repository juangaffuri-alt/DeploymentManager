using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PasadorScript.Helpers;
using PasadorScript.Repository;

namespace PasadorScript.Controllers
{
    public class RuntimeHelperController : Controller
    {
        private readonly IDbRepository _dbRepository;

        public RuntimeHelperController(IDbRepository dbRepository)
        {
            _dbRepository = dbRepository;
        }
        [HttpGet]
        public async Task<IActionResult> BuscarUsuario(string term, TablasType tablasType)
        {
            if (string.IsNullOrWhiteSpace(term))
                return Json(new List<string>());

            var resultados = await _dbRepository.GetUsuarios(tablasType, term);

            return Json(resultados);
        }
    }
}
