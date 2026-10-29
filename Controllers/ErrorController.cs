using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace PasadorScript.Controllers
{
    public class ErrorController : Controller
    {
        [Route("Error/Unauthorized")]
        public IActionResult Unauthorized()
        {
            return View("~/Views/Error/Unauthorized.cshtml");
        }

        [Route("Error/HandleError/{statusCode}")]
        public IActionResult HandleError(int statusCode)
        {
            return statusCode switch
            {
                403 => View("Unauthorized"),
                404 => View("NotFound"),
                _ => View("GenericError")
            };
        }
    }
}
