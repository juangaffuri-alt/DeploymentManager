using Microsoft.AspNetCore.Mvc;
using PasadorScript.Services.Cache;

namespace PasadorScript.Controllers
{
    public class CacheController : Controller
    {
        private readonly IDevOpsCacheService _cacheService;

        public CacheController(IDevOpsCacheService cacheService) 
        {
            _cacheService = cacheService;
        }
        [HttpGet]
        public IActionResult CacheStats()
        {
            if (_cacheService is DevOpsCacheService cacheService)
            {
                var stats = cacheService.GetCacheStatistics();
                return Json(stats);
            }
            return Json(new { error = "No se pueden obtener estadísticas" });
        }
    }
}
