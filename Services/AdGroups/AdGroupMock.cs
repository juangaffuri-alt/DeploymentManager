using Castle.Components.DictionaryAdapter.Xml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PasadorScript.DbModels;
using PasadorScript.DTO;
using PasadorScript.Services.RoleMapper;
using System.Security.Claims;


namespace PasadorScript.Services.AdGroups
{
    public class AdGroupMock:IAdGroupService
    {
        private readonly string[] mockGroups = new string[] { "DBA"};
    private readonly NuevoP229Context _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AdGroupMock> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private const string CACHE_KEY = "ad_group_mappings";
        private const string CACHE_AVATAR = "";
        private readonly TimeSpan CACHE_DURATION = TimeSpan.FromMinutes(30);

        public AdGroupMock(NuevoP229Context context, IMemoryCache cache, ILogger<AdGroupMock> logger, 
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<Dictionary<string, string>> GetGroupMappingsAsync()
        {
            if (_cache.TryGetValue(CACHE_KEY, out Dictionary<string, string>? cached))
            {
                return cached!;
            }

            try
            {
                var mappings = await _context.AdGroupMappings
                    .Where(x => x.IsActive)
                    .ToDictionaryAsync(x => x.GroupGuid, x => x.GroupName);

                // Si la cache tiene SizeLimit, es obligatorio especificar Size en las opciones.
                var entryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = CACHE_DURATION,
                    Size = Math.Max(1, mappings.Count) // tamaño mínimo 1
                };

                _cache.Set(CACHE_KEY, mappings, entryOptions);
                return mappings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo mapeos de grupos AD");
                return new Dictionary<string, string>();
            }
        }

        public async Task<AuthUserAd> GetInfoUser()
        {
            var userId = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userEmail = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Email)?.Value;
            var familyName = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Surname)?.Value;
            var dbGroups = _context.AdGroupMappings.Where(x => mockGroups.Contains(x.GroupName)).Select(x => new AuthRoleAd { Guid = x.GroupGuid, Name = x.Description!, GroupName = x.GroupName! });

            var user = new AuthUserAd()
            {
                Username = userId!,
                Email = userEmail!,
                FamilyName = familyName!,
                Roles = dbGroups.ToList()
            };
            return user;
        }

        public async Task<List<string>> GetUserGroupNamesAsync(IEnumerable<string> roleGuids)
        {
            var mappings = await GetGroupMappingsAsync();
            return roleGuids.Where(mappings.ContainsKey).Select(guid => mappings[guid]).ToList();
        }

        public async Task<bool> UserHasGroupAsync(IEnumerable<string> roleGuids, string groupName)
        {
            var mappings = await GetGroupMappingsAsync();
            var groupGuid = mappings.FirstOrDefault(x => x.Value == groupName).Key;
            return !string.IsNullOrEmpty(groupGuid) && roleGuids.Contains(groupGuid);
        }

        public async Task<AdGroupMapping?> GetGroupByNameAsync(string groupName)
        {
            return await _context.AdGroupMappings
                .FirstOrDefaultAsync(x => x.GroupName == groupName && x.IsActive);
        }

        public async Task<List<AdGroupMapping>> GetAllActiveGroupsAsync()
        {
            return await _context.AdGroupMappings
                .Where(x => x.IsActive)
                .OrderBy(x => x.GroupName)
                .ToListAsync();
        }

        public void InvalidateCache(string? userId)
        {
            _cache.Remove(CACHE_KEY);
            if (!String.IsNullOrEmpty(userId))
                _cache.Remove($"{CACHE_AVATAR}{userId}");
        }

        public Task<List<AuthRoleAd>> GetUserGroupNamesAsync()
        {
            throw new NotImplementedException();
        }
    }
}
