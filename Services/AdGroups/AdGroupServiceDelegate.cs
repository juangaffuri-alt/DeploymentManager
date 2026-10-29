using Castle.Components.DictionaryAdapter.Xml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PasadorScript.DbModels;
using PasadorScript.DTO;
using PasadorScript.Services.Graph;
using System.Net.Http.Headers;
using System;
using System.Security.Claims;

namespace PasadorScript.Services.AdGroups
{
    public class AdGroupServiceDelegate : IAdGroupService
    {
        private readonly NuevoP229Context _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AdGroupServiceDelegate> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IServiciosGraph _serviciosGraph;
        private readonly IConfiguration _configuration;
        private const string CACHE_KEY = "ad_group_mappings";
        private const string CACHE_AVATAR = "avatarImg";
        private readonly TimeSpan CACHE_DURATION = TimeSpan.FromMinutes(30);

        public AdGroupServiceDelegate(NuevoP229Context context, IMemoryCache cache, ILogger<AdGroupServiceDelegate> logger, 
            IHttpContextAccessor httpContextAccessor, IServiciosGraph serviciosGraph, IConfiguration configuration)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _serviciosGraph = serviciosGraph;
            _configuration = configuration;
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
                    .ToDictionaryAsync(x => x.GroupGuid, x => x.GroupName!);

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

        public async Task<byte[]?> GetImage(string url)
        {
            using var client = new HttpClient();
            var _pat = _configuration["AzureSettings:PersonalAccessToken"];
            var credentials = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($":{_pat}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            var bytes = await response.Content.ReadAsByteArrayAsync();

            return bytes;
        }

        public async Task<AuthUserAd> GetInfoUser()
        {
            var userId = _httpContextAccessor.HttpContext?.User.FindFirst("preferred_username")?.Value;
            var fullName = _httpContextAccessor.HttpContext?.User.FindFirst("name")?.Value;
            var userEmail = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Email)?.Value;
            var familyName = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.GivenName)?.Value;
            var roleGuids = _httpContextAccessor.HttpContext?.User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value);

            // Intentar obtener avatar del caché
            byte[]? avatarBytes = null;
            string mailAddress = null;
            if (userId != null)
            {
                _cache.TryGetValue($"{CACHE_AVATAR}{userId}", out avatarBytes);
                _cache.TryGetValue($"mail{userId}", out mailAddress);

                if (avatarBytes == null || avatarBytes.Length == 0)
                {
                    var usrGraph = await _serviciosGraph.InfoUser(userId!);
                    mailAddress = usrGraph?.mailAddress;
                    if (usrGraph != null && usrGraph._links?.avatar?.href != null)
                    {
                        avatarBytes = await GetImage(usrGraph._links.avatar.href);

                        // Solo guardar en caché si tenemos bytes válidos
                        if (avatarBytes != null && avatarBytes.Length> 0)
                        {
                            _cache.Set($"{CACHE_AVATAR}{userId}", avatarBytes, new MemoryCacheEntryOptions
                            {
                                AbsoluteExpirationRelativeToNow = CACHE_DURATION,
                                Size = avatarBytes.Length // Opcional: para tracking de tamaño
                            });
                            _cache.Set($"mail{userId}", mailAddress, new MemoryCacheEntryOptions
                            {
                                AbsoluteExpirationRelativeToNow = CACHE_DURATION,
                                Size = mailAddress.Length // Opcional: para tracking de tamaño
                            });
                        }
                    }
                }
            }

            
            var groupNames = await GetUserGroupNamesAsync();
            
            var user = new AuthUserAd()
            {
                Username = userId!,
                FullName = fullName!,
                Email = userEmail!,
                FamilyName = familyName!,
                Avatar = avatarBytes, // Puede ser null
                Roles = groupNames,
                EmailAzure = mailAddress!
            };
            return user;
        }

        public async Task<List<string>> GetUserGroupNamesAsync(IEnumerable<string> roleGuids)
        {
            return roleGuids.ToList();
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
            var groups = _httpContextAccessor.HttpContext?.User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList() ?? new List<string>();
            var uids = _httpContextAccessor.HttpContext?.User.Claims.Where(c => c.Type == "role_guid").Select(c => c.Value).ToList() ?? new List<string>();
            //role_description
            var descs = _httpContextAccessor.HttpContext?.User.Claims.Where(c => c.Type == "role_description").Select(c => c.Value).ToList() ?? new List<string>();
            var authRoles = new List<AuthRoleAd>();
            int i = 0;
            foreach (var g  in groups)
            {
                if (!uids.Any())
                {
                    break;
                }
                authRoles.Add(new AuthRoleAd
                {
                    GroupName = g,
                    Guid = uids[i] ?? "",
                    Name = descs[i] ?? ""
                });
                i++;
            }
            return Task.FromResult(authRoles);
        }
    }
}
