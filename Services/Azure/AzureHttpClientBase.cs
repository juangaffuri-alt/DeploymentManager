using PasadorScript.DTO.AzureDev;
using System.Net.Http.Headers;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;

namespace PasadorScript.Services.Azure
{
    /// <summary>
    /// Clase base abstracta para clientes HTTP de Azure DevOps
    /// Encapsula la lógica común de caché, autenticación y requests
    /// </summary>
    public abstract class AzureHttpClientBase
    {
        protected readonly IHttpClientFactory Factory;
        protected readonly ILogger Logger;
        protected readonly Dictionary<string, HttpClient> Cache = new();
        public bool IgnoreCaseSensitive { get; set; } = false;

        protected AzureHttpClientBase(IHttpClientFactory factory, ILogger logger)
        {
            Factory = factory;
            Logger = logger;
        }

        /// <summary>
        /// Método abstracto que cada subclase debe implementar para resolver el PAT
        /// </summary>
        protected abstract Task<string> ResolvePat(string pat = null);

        /// <summary>
        /// Obtiene o crea un HttpClient configurado con el PAT
        /// </summary>
        protected async Task<HttpClient> GetClientAsync(string pat = null)
        {
            var resolvedPat = await ResolvePat(pat);

            if (string.IsNullOrEmpty(resolvedPat))
                throw new InvalidOperationException("No se pudo resolver un PAT válido");

            // Verificar cache
            if (Cache.TryGetValue(resolvedPat, out var cached))
            {
                Logger.LogDebug("Using cached HttpClient for PAT");
                return cached;
            }

            // Crear nuevo cliente
            var client = Factory.CreateClient(GetType().Name);
            var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{resolvedPat}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
            
            Cache[resolvedPat] = client;
            Logger.LogDebug("Created new HttpClient with authorization header");
            
            return client;
        }

        /// <summary>
        /// Realiza un GET request genérico con deserialización automática
        /// </summary>
        public async Task<T> GetAsync<T>(string url, string pat = null)
        {
            try
            {
                var http = await GetClientAsync(pat);
                var res = await http.GetAsync(url);
                res.EnsureSuccessStatusCode();
                JsonSerializerOptions opt = new JsonSerializerOptions()
                {
                    PropertyNameCaseInsensitive = IgnoreCaseSensitive
                };
                var result = await res.Content.ReadFromJsonAsync<T>(options:opt);
                if (result == null)
                    throw new InvalidOperationException($"Empty response for {url}");
                
                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in GET request to {Url}", url);
                throw;
            }
        }

        public async Task<HttpResponseMessage> GetAsync(string url, string pat = null)
        {
            try
            {
                var http = await GetClientAsync(pat);
                var res = await http.GetAsync(url);
                res.EnsureSuccessStatusCode();
                return res;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in GET request to {Url}", url);
                throw;
            }
        }

        /// <summary>
        /// Realiza un request genérico (GET, POST, PATCH, etc.)
        /// </summary>
        public async Task<HttpResponseMessage> SendAsync<T>(HttpMethod method, string url, T? body = default, string pat = null)
        {
            try
            {
                var http = await GetClientAsync(pat);
                var req = new HttpRequestMessage(method, url);
                req.Headers.Add("Accept", "application/json");
                if (body != null)
                {
                    var json = JsonSerializer.Serialize(body);
                    req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }
                
                Logger.LogDebug("{Method} {Url}", method, url);
                return await http.SendAsync(req);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in {Method} request to {Url}", method, url);
                throw;
            }
        }

        /// <summary>
        /// Limpia el cache de clientes HTTP
        /// </summary>
        public virtual void ClearCache()
        {
            Cache.Clear();
            Logger.LogInformation("HTTP client cache cleared");
        }
    }
}