using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PasadorScript.Services.AdGroups;
using PasadorScript.Services.Pat;
using PasadorScript.ViewModels;
using System.Threading.Tasks;
namespace PasadorScript.Validators
{
    public class RequireValidPatAttribute : Attribute, IAsyncActionFilter
    {
        private readonly IPatService _patService; // servicio para validar PAT y DB

        public RequireValidPatAttribute()
        {
            // No inyectamos directamente aquí porque el atributo no es servicio,
            // se usará el service locator en OnActionExecutionAsync
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Obtener el servicio desde el contexto
            var patService = context.HttpContext.RequestServices.GetService(typeof(IPatService)) as IPatService;
            var adGroupService = context.HttpContext.RequestServices.GetService(typeof(IAdGroupService)) as IAdGroupService;
            var usr = await adGroupService.GetInfoUser();
            var userName = usr.Username;
            if (string.IsNullOrEmpty(userName))
            {
                // Si no está logueado, dejar seguir para que la autenticación maneje esto
                await next();
                return;
            }

            bool patExistsAndValid = await patService.UserHasValidPatAsync(userName);

            if (!patExistsAndValid)
            {
                // Redirigir a vista para cargar el PAT
                var returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
                var model = new PatView();
                context.Result = new RedirectToActionResult("UploadPat", "Account", new { model = model, returnUrl = returnUrl });
                return;
            }

            await next();
        }
    }
}

