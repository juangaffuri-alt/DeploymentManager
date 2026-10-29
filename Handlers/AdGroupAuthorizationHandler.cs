using Microsoft.AspNetCore.Authorization;
using PasadorScript.Roles;
using PasadorScript.Services.AdGroups;
using System.Security.Claims;

namespace PasadorScript.Handlers
{
    public class AdGroupAuthorizationHandler : AuthorizationHandler<AdGroupRequirement>
    {
        private readonly IAdGroupService _adGroupService;
        private readonly ILogger<AdGroupAuthorizationHandler> _logger;

        public AdGroupAuthorizationHandler(IAdGroupService adGroupService, ILogger<AdGroupAuthorizationHandler> logger)
        {
            _adGroupService = adGroupService;
            _logger = logger;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AdGroupRequirement requirement)
        {
            try
            {
                // Obtener todos los claims de "role" del JWT
                var roleClaims = context.User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value);

                // Verificar si el usuario pertenece al grupo requerido
                if (await _adGroupService.UserHasGroupAsync(roleClaims, requirement.GroupName))
                {
                    context.Succeed(requirement);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verificando autorización para grupo {GroupName}", requirement.GroupName);
                // No hacer Succeed() en caso de error para mantener la seguridad
            }
        }
    }
}
