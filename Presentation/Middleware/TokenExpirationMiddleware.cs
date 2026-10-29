using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using PasadorScript.Helpers;
using PasadorScript.DbModels;
using PasadorScript.Repository;
using PasadorScript.Services.Token;
using PasadorScript.Services.User;
using PasadorScript.Services.AdGroups;
using PasadorScript.Services.Pat;
using PasadorScript.Services.Crypto;

namespace PasadorScript.Presentation.Middleware
{
    public class TokenExpirationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TokenExpirationMiddleware> _logger;

        public TokenExpirationMiddleware(RequestDelegate next, ILogger<TokenExpirationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task SetPatInService(IUserContext userContext, IAdGroupService adGroupService, IPatRepositoryService patRepositoryService)
        {
            var sg = await adGroupService.GetInfoUser();

            var pat = await patRepositoryService.GetPatByUser(sg.Username);

            if (userContext is UserContext ctx)
            {
                ctx.PersonalAccessToken = pat;
                ctx.UserId = sg.Username;
            }
        }

        public async Task InvokeAsync(HttpContext context, ITokenService tokenService, IPatRepositoryService patRepositoryService,
            TempDataHelper tempDataHelper, IUserContext userContext, IAdGroupService adGroupService, IDbRepository _dbRepository, IEncriptacionHelper _encriptacionHelper)
        {
            var usr = await adGroupService.GetInfoUser();
            //logging
            using (Serilog.Context.LogContext.PushProperty("RequestId", context.TraceIdentifier))
            using (Serilog.Context.LogContext.PushProperty("UserId", usr!=null? usr.Username:"anonymous"))
            using (Serilog.Context.LogContext.PushProperty("Service", context.Request.Path))
            {
                // Solo verificar en rutas autenticadas
                if (context.User?.Identity?.IsAuthenticated == true)
                {

                    var token = await tokenService.GetValidAccessTokenAsync();
                    if (string.IsNullOrEmpty(token))
                    {
                        if (context.Request.Path.StartsWithSegments("/Account/CheckSession"))
                        {
                            context.Response.StatusCode = 401;
                            await context.Response.WriteAsync("Sesión expirada");
                            return;
                        }

                        var referer = context.Request.Headers["Referer"].ToString();
                        _logger.LogInformation("Token expirado o inválido para usuario {User}", context.User.Identity.Name);

                        // Limpiar la sesión
                        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        context.Session.Clear();

                        // Si es una petición AJAX, devolver 401
                        if (IsAjaxRequest(context.Request))
                        {
                            context.Response.StatusCode = 401;
                            await context.Response.WriteAsync("Sesión expirada");
                            return;
                        }

                        // Para peticiones normales, redirigir con mensaje
                        await tempDataHelper.Message(TypeNotify.Warning,
                            "Su sesión ha expirado. Por favor, inicie sesión nuevamente.",
                            "Sesión Expirada");

                        //context.Response.Redirect("/Home/Index");
                        await _next(context);
                        return;
                    }
                    await SetPatInService(userContext, adGroupService, patRepositoryService);
                }

                await _next(context);
            }

            
        }

        private static bool IsAjaxRequest(HttpRequest request)
        {
            return request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                   request.Headers["Accept"].ToString().Contains("application/json");
        }
    }
}
