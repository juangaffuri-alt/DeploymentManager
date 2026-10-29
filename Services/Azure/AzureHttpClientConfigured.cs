using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PasadorScript.Services.Azure
{
    /// <summary>
    /// HttpClient que usa PAT desde appsettings.json
    /// Uso: Operaciones del sistema que NO dependen de usuario específico
    /// Ciclo de vida: Singleton
    /// </summary>
    public class AzureHttpClientConfigured : AzureHttpClientBase
    {
        private readonly IConfiguration _config;

        public AzureHttpClientConfigured(
            IHttpClientFactory factory,
            ILogger<AzureHttpClientConfigured> logger,
            IConfiguration config)
            : base(factory, logger)
        {
            _config = config;
        }

        /// <summary>
        /// Resuelve el PAT desde appsettings.json
        /// El parámetro pat es ignorado (siempre usa config)
        /// </summary>
        protected override Task<string> ResolvePat(string pat = null)
        {
            var configPat = _config["AzureSettings:PersonalAccessToken"];

            if (string.IsNullOrEmpty(configPat))
            {
                Logger.LogError("PAT not found in appsettings.json at 'AzureSettings:PersonalAccessToken'");
                throw new InvalidOperationException(
                    "No se encontró 'AzureSettings:PersonalAccessToken' en appsettings.json");
            }

            Logger.LogDebug("Using PAT from appsettings.json");
            return Task.FromResult(configPat);
        }

        /// <summary>
        /// Sobrescribe SendAsync para ignorar el parámetro pat (siempre usa config)
        /// </summary>
        public new async Task<HttpResponseMessage> SendAsync<T>(HttpMethod method, string url, T? body = default)
        {
            return await base.SendAsync(method, url, body, null);
        }

        /// <summary>
        /// Sobrescribe GetAsync para ignorar el parámetro pat (siempre usa config)
        /// </summary>
        public new async Task<T> GetAsync<T>(string url)
        {
            return await base.GetAsync<T>(url, null);
        }
    }
}