using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;

namespace PasadorScript.Presentation.Middleware
{
    public class ReturnUrlFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var controller = context.Controller as Controller;
            if (controller != null)
            {
                var referer = controller.Request.Headers["Referer"].ToString();
                controller.ViewBag.ReturnUrl = string.IsNullOrEmpty(referer)
                    ? controller.Url.Action("Index", "Home")
                    : referer;
            }
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
