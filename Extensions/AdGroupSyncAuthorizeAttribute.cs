using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;

namespace PasadorScript.Extensions
{
    /// <summary>
    /// Versión simplificada que usa solo los claims (más rápida, sin llamadas async al servicio)
    /// </summary>
    public class AdGroupSyncAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        private readonly string[] _groupNames;
        private readonly bool _requireAll;

        public AdGroupSyncAuthorizeAttribute(params string[] groupNames)
        {
            _groupNames = groupNames;
            _requireAll = false;
        }

        public AdGroupSyncAuthorizeAttribute(bool requireAll, params string[] groupNames)
        {
            _groupNames = groupNames;
            _requireAll = requireAll;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;

            if (!user.Identity.IsAuthenticated)
            {
                context.Result = new ChallengeResult();
                return;
            }

            var userGroups = user.GetAdGroups().ToList();

            bool hasAccess = false;

            if (_requireAll)
            {
                hasAccess = _groupNames.All(group => userGroups.Contains(group));
            }
            else
            {
                hasAccess = _groupNames.Any(group => userGroups.Contains(group));
            }

            if (!hasAccess)
            {
                context.Result = new ForbidResult();
            }
        }
    }
}
