using Microsoft.AspNetCore.Authorization;

namespace PasadorScript.Services.PolicyConfiguration
{
    public interface IPolicyConfigurationService
    {
        Task ConfigurePoliciesAsync(AuthorizationOptions options);
    }
}