using Newtonsoft.Json;
using PasadorScript.DbModels;
using PasadorScript.DTO.AzureDev;
using PasadorScript.DTO.Graph;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PasadorScript.Services.Graph
{
    public class ServiciosGraph : IServiciosGraph
    {
        private readonly ILogger<ServiciosGraph> _logger;
        private readonly IConfiguration _configuration;
        private readonly string? _pat;
        private readonly HttpClient _httpClient;
        public ServiciosGraph(ILogger<ServiciosGraph> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _pat = configuration["AzureSettings:PersonalAccessToken"];
            _httpClient = new HttpClient();
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{_pat}"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }

        public async Task<UserInfo?> InfoUser(string directoryAlias)
        {
            string url = GetUriFormatted("ApiAllUsers");
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Error al obtener usuarios: {response.StatusCode}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var resp = JsonConvert.DeserializeObject<UsersResponse>(json);
            return resp?.value?.FirstOrDefault(x => String.Equals(x.directoryAlias, directoryAlias, StringComparison.OrdinalIgnoreCase));

        }

        public async Task<ICollection<string>> GroupsByUser(string descriptorUser)
        {
            string url = GetUriFormatted("ApiDetailMembership", descriptorUser);
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Error al obtener usuarios: {response.StatusCode}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var resp = JsonConvert.DeserializeObject<Membership>(json);
            ICollection<string> res = new List<string>();
            // Leer la sección de roles como un diccionario
            var descriptors = _configuration.GetSection("AzureSettings:DescriptorsGroup")
                                    .Get<Dictionary<string, string>>();


            foreach (var item in descriptors)
            {
                if (resp.value.FirstOrDefault(x => x.containerDescriptor == item.Value) != null)
                    res.Add(item.Key);
            }
            return res;
        }


        private string GetUriFormatted(string keyValueInEndpointSetting, params string[] args)
        {
            string? host = _configuration["AzureSettings:EndpointsGraph:Host"];
            string? organization = Uri.EscapeDataString(_configuration["AzureSettings:Organization"]);
            string? formattedUrl = _configuration[$"AzureSettings:EndpointsGraph:{keyValueInEndpointSetting}"];
            object[] finalArgs = new object[] { host, organization }
            .Concat(args.Cast<object>()) // args debe ser de tipo object[] o algo que puedas castear
            .ToArray();

            // Aplicamos el formato con todos los argumentos
            return string.Format(formattedUrl, finalArgs);
        }

        private string GetHostAndProject()
        {
            string? host = _configuration["AzureSettings:EndpointsGraph:Host"];
            string? organization = Uri.EscapeDataString(_configuration["AzureSettings:Organization"]!);
            return $"{host}/{organization}";

        }

        public async Task<GraphUserResponse> GetMyIdAsAsync()
        {
            var profileUrl = GetUriFormatted("ApiMyId");
            var profile = await _httpClient.GetFromJsonAsync<GraphUserResponse>(profileUrl);
            return profile;
        }

        public async Task<GraphUserResponse> GetMyIdAsAsync(string pat)
        {
            var profileUrl = GetUriFormatted("ApiMyId");
            using var httpClient = new HttpClient();
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            var profile = await httpClient.GetFromJsonAsync<GraphUserResponse>(profileUrl);
            return profile;
        }

        public async Task<GraphUserResponse> GetUserByAdName(string pat, string activeDirectoryName)
        {
            using var httpClient = new HttpClient();
            // Autenticación con PAT
            var profileUrl = GetUriFormatted("ApiMyId");
            var patToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", patToken);
            var profile = await httpClient.GetFromJsonAsync<GraphUserResponse>(profileUrl);
            var allUsers = await InfoUser(activeDirectoryName);
            if (allUsers != null)
            {
                profile.Descriptor = allUsers.descriptor;
            }
            return profile;
        }


        public async Task<bool> UsuarioPerteneceAGrupoAsync(string directoryAlias, string groupDescriptor)
        {
            var usr = await InfoUser(directoryAlias);
            if (usr == null)
                return false;

            var baseUrl = GetHostAndProject();
            var url = $"{baseUrl}/_apis/graph/memberships/{usr.descriptor}/{groupDescriptor}?api-version=7.1-preview.1";
            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            // Analizar si hay error de "membership not found"
            if (content.Contains("GraphMembershipNotFoundException"))
            {
                return false;
            }

            return true;
        }

        private string GetUriFormattedHostOnly(string keyValueInEndpointSetting)
        {
            string? host = _configuration["AzureSettings:EndpointsGraph:Host"];
            string? formattedUrl = _configuration[$"AzureSettings:EndpointsGraph:{keyValueInEndpointSetting}"];

            // Aplicamos el formato con todos los argumentos
            string res = string.Format(formattedUrl, host);
            return res;

        }

        public async Task<UserInfo?> InfoUserByEmail(string email)
        {
            string url = GetUriFormatted("ApiAllUsers");
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Error al obtener usuarios: {response.StatusCode}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var resp = JsonConvert.DeserializeObject<UsersResponse>(json);
            return resp?.value?.FirstOrDefault(x => String.Equals(x.mailAddress, email, StringComparison.OrdinalIgnoreCase) || String.Equals(x.principalName, email, StringComparison.OrdinalIgnoreCase));
        }
        public async Task<UserInfo?> InfoUserByDisplayName(string displayName)
        {
            string url = GetUriFormatted("ApiAllUsers");
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Error al obtener usuarios: {response.StatusCode}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var resp = JsonConvert.DeserializeObject<UsersResponse>(json);
            return resp?.value?.FirstOrDefault(x => String.Equals(x.displayName, displayName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
