using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PasadorScript.Roles
{
    public class AdGroupRequirement : IAuthorizationRequirement
    {
        public string GroupName { get; }

        public AdGroupRequirement(string groupName)
        {
            GroupName = groupName;
        }
    }
}
