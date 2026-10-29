using Microsoft.EntityFrameworkCore;
using PasadorScript.DbModels;
using PasadorScript.DTO.AzureDev;
using PasadorScript.DTO.Sugus;
using System.Text.Json;

namespace PasadorScript.Services.RoleMapper
{
    public class RoleMapperService : IRoleMapperService
    {
        private readonly IConfiguration _configuration;
        private readonly NuevoP229Context _context;

        public RoleMapperService(IConfiguration configuration, NuevoP229Context context)
        {
            _configuration = configuration;
            _context = context;
        }
        public async Task<List<string>> MapGroupsToRolesAsync(string userId)
        {
            var list = new List<string>();
            var host = _configuration["OauthConfig:Authority"]!;
            var path = _configuration["OauthConfig:EnpointRoles"]!;

            var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(String.Format($"{host}{path}", userId));
            if (response.IsSuccessStatusCode)
            {
                var r = await response.Content.ReadAsStringAsync();


                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true // Ignora mayúsculas/minúsculas
                };

                var prInfo = JsonSerializer.Deserialize<ICollection<RoleAD>>(r, options);
                list = prInfo?.Select(x => x.Guid).ToList();
            }
            var listReales = _context.AdGroupMappings.Where(x => list.Contains(x.GroupGuid)).Select(x => x.GroupGuid).ToList();
            
            return listReales;
        }
    }
}
