using PasadorScript.Services.AdGroups;
using System.Security.Claims;

namespace PasadorScript.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        /// <summary>
        /// Reemplaza User.IsInRole() usando tu servicio de AD Groups
        /// </summary>
        public static async Task<bool> IsInAdGroupAsync(this ClaimsPrincipal user, IAdGroupService adGroupService, string groupName)
        {
            if (!user.Identity.IsAuthenticated)
                return false;

            var roleClaims = user.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value);
            return await adGroupService.UserHasGroupAsync(roleClaims, groupName);
        }

        /// <summary>
        /// Verifica si el usuario pertenece a cualquiera de los grupos especificados
        /// </summary>
        public static async Task<bool> IsInAnyAdGroupAsync(this ClaimsPrincipal user, IAdGroupService adGroupService, params string[] groupNames)
        {
            if (!user.Identity.IsAuthenticated)
                return false;

            var roleClaims = user.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value);

            foreach (var groupName in groupNames)
            {
                if (await adGroupService.UserHasGroupAsync(roleClaims, groupName))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Verifica si el usuario pertenece a todos los grupos especificados
        /// </summary>
        public static async Task<bool> IsInAllAdGroupsAsync(this ClaimsPrincipal user, IAdGroupService adGroupService, params string[] groupNames)
        {
            if (!user.Identity.IsAuthenticated)
                return false;

            var roleClaims = user.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value);

            foreach (var groupName in groupNames)
            {
                if (!await adGroupService.UserHasGroupAsync(roleClaims, groupName))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Obtiene todos los grupos AD del usuario
        /// </summary>
        public static IEnumerable<string> GetAdGroups(this ClaimsPrincipal user)
        {
            return user.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value);
        }

        /// <summary>
        /// Versión síncrona para casos donde ya tienes los grupos cacheados
        /// </summary>
        public static bool IsInAdGroupSync(this ClaimsPrincipal user, string groupName)
        {
            return user.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == groupName);
        }
    }
}
