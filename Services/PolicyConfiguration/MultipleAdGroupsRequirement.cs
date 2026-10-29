using Microsoft.AspNetCore.Authorization;

namespace PasadorScript.Services.PolicyConfiguration
{
    public class MultipleAdGroupsRequirement : IAuthorizationRequirement
    {
        public IEnumerable<string> GroupNames { get; }

        public MultipleAdGroupsRequirement(IEnumerable<string> groupNames)
        {
            GroupNames = groupNames ?? new List<string>();
        }
    }
}
