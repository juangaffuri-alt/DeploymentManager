using Microsoft.AspNetCore.Authorization;
using PasadorScript.Roles;
using PasadorScript.Services.AdGroups;

namespace PasadorScript.Services.PolicyConfiguration
{
    public class PolicyConfigurationService : IPolicyConfigurationService
    {
        private readonly IServiceProvider _serviceProvider;

        public PolicyConfigurationService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task ConfigurePoliciesAsync(AuthorizationOptions options)
        {
            using var scope = _serviceProvider.CreateScope();
            var adGroupService = scope.ServiceProvider.GetRequiredService<IAdGroupService>();

            var activeGroups = await adGroupService.GetAllActiveGroupsAsync();

            foreach (var group in activeGroups)
            {
                options.AddPolicy(group.GroupName, policy =>
                    policy.Requirements.Add(new AdGroupRequirement(group.GroupName)));
            }
        }
    }

}
