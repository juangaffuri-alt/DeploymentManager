using PasadorScript.DbModels;
using PasadorScript.DTO;

namespace PasadorScript.Services.AdGroups
{
    public interface IAdGroupService
    {
        Task<List<AdGroupMapping>> GetAllActiveGroupsAsync();
        Task<AdGroupMapping?> GetGroupByNameAsync(string groupName);
        Task<Dictionary<string, string>> GetGroupMappingsAsync();
        Task<List<string>> GetUserGroupNamesAsync(IEnumerable<string> roleGuids);
        Task<List<AuthRoleAd>> GetUserGroupNamesAsync();
        void InvalidateCache(string? userId);
        Task<bool> UserHasGroupAsync(IEnumerable<string> roleGuids, string groupName);
        Task<AuthUserAd> GetInfoUser();
    }
}