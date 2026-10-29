using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PasadorScript.Services.Azure
{
    /// <summary>
    /// Cliente HTTP original compatible con la firma antigua de métodos
    /// Mantiene compatibilidad hacia atrás: SendAsync(pat, method, url, body)
    /// Internamente usa AzureHttpClientConfigured
    /// </summary>
    public class AzureHttpClientLegacy
    {
        private readonly IHttpClientFactory _factory;
        private readonly ILogger<AzureHttpClientLegacy> _logger;
        private readonly Dictionary<string, HttpClient> _cache = new();

        public AzureHttpClientLegacy(IHttpClientFactory factory, ILogger<AzureHttpClientLegacy> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        private HttpClient GetClient(string pat)
        {
            if (_cache.TryGetValue(pat, out var cached))
                return cached;

            var client = _factory.CreateClient(nameof(AzureHttpClientLegacy));
            var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
            _cache[pat] = client;
            return client;
        }

        /// <summary>
        /// Firma antigua: GetAsync(pat, url)
        /// </summary>
        public async Task<T> GetAsync<T>(string pat, string url)
        {
            var http = GetClient(pat);
            var res = await http.GetAsync(url);
            res.EnsureSuccessStatusCode();
            return await res.Content.ReadFromJsonAsync<T>() ?? throw new InvalidOperationException($"Empty response for {url}");
        }

        /// <summary>
        /// Firma antigua: SendAsync(pat, method, url, body)
        /// </summary>
        public async Task<HttpResponseMessage> SendAsync<T>(string pat, HttpMethod method, string url, T? body = default)
        {
            var http = GetClient(pat);
            var req = new HttpRequestMessage(method, url);
            if (body != null)
            {
                var json = JsonSerializer.Serialize(body);
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }
            _logger.LogDebug($"{method} {url}");
            return await http.SendAsync(req);
        }
    }
}