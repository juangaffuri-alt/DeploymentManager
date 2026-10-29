// MultipleAdGroupsHandler.cs
using Microsoft.AspNetCore.Authorization;
using PasadorScript.Services.AdGroups;
using System.Security.Claims;

namespace PasadorScript.Services.PolicyConfiguration
{
    public class MultipleAdGroupsHandler : AuthorizationHandler<MultipleAdGroupsRequirement>
    {
        private readonly IAdGroupService _adGroupService;
        private readonly ILogger<MultipleAdGroupsHandler> _logger;

        public MultipleAdGroupsHandler(
            IAdGroupService adGroupService,
            ILogger<MultipleAdGroupsHandler> logger)
        {
            _adGroupService = adGroupService;
            _logger = logger;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            MultipleAdGroupsRequirement requirement)
        {
            try
            {
                // Obtener los GUIDs de los roles del usuario desde los claims
                var roleGuids = context.User.Claims
                    .Where(c => c.Type == ClaimTypes.Role)
                    .Select(c => c.Value)
                    .ToList();

                if (!roleGuids.Any())
                {
                    _logger.LogWarning("Usuario sin roles claims");
                    return;
                }

                // Obtener los mappings de grupos AD
                var mappings = await _adGroupService.GetGroupMappingsAsync();

                // Verificar si el usuario pertenece a ALGUNO de los grupos requeridos
                foreach (var requiredGroup in requirement.GroupNames)
                {
                    var groupGuid = mappings.FirstOrDefault(x => x.Value == requiredGroup).Key;

                    if (!string.IsNullOrEmpty(groupGuid) && roleGuids.Contains(groupGuid))
                    {
                        // El usuario pertenece al menos a uno de los grupos requeridos
                        context.Succeed(requirement);
                        return;
                    }
                }

                _logger.LogInformation("Usuario no pertenece a ninguno de los grupos requeridos: {Groups}",
                    string.Join(", ", requirement.GroupNames));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validando múltiples grupos AD");
            }
        }
    }
}