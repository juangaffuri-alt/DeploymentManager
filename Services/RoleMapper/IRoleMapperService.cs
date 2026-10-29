
namespace PasadorScript.Services.RoleMapper
{
    public interface IRoleMapperService
    {
        Task<List<string>> MapGroupsToRolesAsync(string userId);
    }
}