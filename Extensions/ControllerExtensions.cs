using Microsoft.AspNetCore.Mvc;

namespace PasadorScript.Extensions
{
    public static class ControllerExtensions
    {
        public static string GetReturnUrl(this Controller controller, string? fallback = null)
        {
            var referer = controller.Request.Headers["Referer"].ToString();

            if (!string.IsNullOrEmpty(referer))
                return referer;

            return !string.IsNullOrEmpty(fallback)
                ? fallback
                : controller.Url.Action("Index", "Home")!;
        }
    }
}
