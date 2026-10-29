using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PasadorScript.Repository;
using PasadorScript.ViewModels;
using PasadorScript.Helpers;
using System.Security.Claims;
using PasadorScript.Services.Azure;
using PasadorScript.Services.Token;
using PasadorScript.Services.Pat;
using PasadorScript.Services.User;
using PasadorScript.Services.AdGroups;
using PasadorScript.Services.Crypto;

namespace PasadorScript.Controllers
{
    public class AccountController : Controller
    {
        private readonly ILogger<AccountController> _logger;
        private readonly IDbRepository _dbRepository;
        private readonly IConfiguration _configuration;
        private readonly TempDataHelper _tempDataHelper;
        private readonly ITokenService _tokenService;
        private readonly IAdGroupService _adGroupService;
        private readonly IEncriptacionHelper _encriptacionHelper;
        private readonly IAzurePatService _azurePatService;

        public AccountController(ILogger<AccountController> logger,
            IDbRepository dbRepository, IAzurePatService azurePatService,
            IConfiguration configuration, TempDataHelper tempDataHelper, ITokenService tokenService, 
            IAdGroupService adGroupService, IEncriptacionHelper encriptacionHelper)
        {
            _logger = logger;
            _dbRepository = dbRepository;
            _configuration = configuration;
            _tempDataHelper = tempDataHelper;
            _tokenService = tokenService;
            _adGroupService = adGroupService;
            _encriptacionHelper = encriptacionHelper;
            _azurePatService = azurePatService;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> UserInfo()
        {
            var uad = await _adGroupService.GetInfoUser();
            var userId = uad.Username;
            var userEmail = uad.Email;
            var roleGuids = uad.Roles.Select(x=>x.Guid);

            var groupNames = await _adGroupService.GetUserGroupNamesAsync(roleGuids);

            return Json(new
            {
                UserId = userId,
                Email = userEmail,
                Groups = groupNames
            });
        }
      

        [HttpGet]
        public async Task<IActionResult> Signin(string returnUrl = "/")
        {
            return Challenge(new AuthenticationProperties { RedirectUri = returnUrl }, "OpenIdConnect");
        }

        
        [HttpGet]
        public async Task<IActionResult> SignOut()
        {
            await _tempDataHelper.Message(TypeNotify.Success, $"Sesión finalizada exitosamente", "Logout");
            return SignOut(OpenIdConnectDefaults.AuthenticationScheme, CookieAuthenticationDefaults.AuthenticationScheme);
            //// Mostrar mensaje
            //await _tempDataHelper.Message(TypeNotify.Success, $"Sesión finalizada exitosamente", "Logout");
            //await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            //await HttpContext.SignOutAsync("OpenIdConnect");
            //return RedirectToAction("Index", "Home");
            //// Limpiar sesión local
            //HttpContext.Session.Clear();

            //// Mostrar mensaje
            //await _tempDataHelper.Message(TypeNotify.Success, $"Sesión finalizada exitosamente", "Logout");

            //// SignOut completo
            //return SignOut(new AuthenticationProperties
            //{
            //    RedirectUri = "/Home/Index"
            //}, CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CambiarPerfil(string? redirectTo)
        {
            if (!ModelState.IsValid)
            {
                await _tempDataHelper.Message(TypeNotify.Error,
                    "Datos de perfil inválidos.",
                    "Error");
                return RedirectToAction("Index", "Home");
            }

            try
            {
                // Verificar que la sesión sea válida antes de cambiar perfil
                if (!await _tokenService.IsSessionValid())
                {
                    await _tempDataHelper.Message(TypeNotify.Warning,
                        "Su sesión ha expirado. Por favor, inicie sesión nuevamente.",
                        "Sesión Expirada");
                    return RedirectToAction("Signin");
                }

                if (!string.IsNullOrEmpty(redirectTo))
                    return Redirect(redirectTo);

                return RedirectToAction("Index", "Home");
            }
            catch (UnauthorizedAccessException)
            {
                await _tempDataHelper.Message(TypeNotify.Warning,
                    "Su sesión ha expirado. Por favor, inicie sesión nuevamente.",
                    "Sesión Expirada");
                return RedirectToAction("Signin");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing profile");
                await _tempDataHelper.Message(TypeNotify.Error,
                    "Ocurrió un error al cambiar el perfil.",
                    "Error");
                return RedirectToAction("Index", "Home");
            }
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> UploadPat(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            PatView path = new PatView();
            return View(path);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> DeletePat(string? returnUrl = null)
        {
            var usr = await _adGroupService.GetInfoUser();
            var userName = usr.Username;
            await _dbRepository.DeletePat(userName);
            await _tempDataHelper.Message(TypeNotify.Success,
                "PAT eliminado correctamente.",
                "Éxito");

            if (!string.IsNullOrEmpty(returnUrl))
                return Redirect(returnUrl);
            else
                return RedirectToAction("Index", "Home");
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> UploadPat(PatView model, string? returnUrl = null)
        {
            try
            {
                var usr = await _adGroupService.GetInfoUser();

                var userName = usr.Username;

                if (ModelState.IsValid)
                {
                    if (await _azurePatService.PatProof(model.Pat))
                    {
                        var encriptPat = _encriptacionHelper.Encriptar(model.Pat);
                        await _dbRepository.SaveNewPat(userName.ToLower(), encriptPat);
                        await _tempDataHelper.Message(TypeNotify.Success,
                            "PAT guardado correctamente.",
                            "Éxito");

                        if (!string.IsNullOrEmpty(returnUrl))
                            return Redirect(returnUrl);
                        else
                            return RedirectToAction("Index", "Home");
                    }
                    else
                    {
                        await _tempDataHelper.Message(TypeNotify.Error,
                            $"El token ingresado no es válido en Azure DevOps. Verifique que sea correcto o que tenga permisos sobre el proyecto '{_configuration["AzureSettings:Project"]}'",
                            "Error al validar PAT");
                        return View(model);
                    }
                }
                else
                {
                    return View(model);
                }
            }
            catch (UnauthorizedAccessException)
            {
                await _tempDataHelper.Message(TypeNotify.Warning,
                    "Su sesión ha expirado. Por favor, inicie sesión nuevamente.",
                    "Sesión Expirada");
                return RedirectToAction("Signin");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading PAT");
                await _tempDataHelper.Message(TypeNotify.Error,
                    "Ocurrió un error al procesar el PAT.",
                    "Error");
                return View(model);
            }
        }

        /// <summary>
        /// Endpoint para verificar si la sesión sigue siendo válida (útil para AJAX)
        /// </summary>
        // Quitar [Authorize] de este método
        [AllowAnonymous] // ← importante
        [HttpGet]
        public async Task<IActionResult> CheckSession()
        {
            
            if (User.Identity?.IsAuthenticated != true)
            {
                return Json(new { status = "no-auth" });
            }

            // Caso 2 o 3: inició sesión → verificar token
            var token = await _tokenService.GetValidAccessTokenAsync();
            if (!string.IsNullOrEmpty(token) && _tokenService.IsTokenValid(token))
            {
                return Json(new { status = "valid" });
            }

            // Caso 3: logueado pero token inválido
            return Json(new { status = "expired" });

        }

        /// <summary>
        /// Endpoint para extender la sesión si es posible
        /// </summary>
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> ExtendSession()
        {
            try
            {
                var token = await _tokenService.GetValidAccessTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return Json(new { success = false, message = "Sesión expirada" });
                }

                return Json(new { success = true, message = "Sesión extendida" });
            }
            catch
            {
                return Json(new { success = false, message = "Error al extender sesión" });
            }
        }
    }
}
