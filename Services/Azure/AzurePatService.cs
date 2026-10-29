using System.Net;

namespace PasadorScript.Services.Azure
{
    public class AzurePatService: AzureBaseService, IAzurePatService
    {
        private readonly ILogger<AzurePatService> _logger;
        private readonly AzureHttpClientConfigured _httpConfigured;
        private readonly AzureHttpClientUser _httpUser;
        private readonly AzureHttpClientLegacy _httpUserLegacy;

        public AzurePatService(IConfiguration configuration, ILogger<AzurePatService> logger,
            AzureHttpClientConfigured azureHttpClientConfigured, AzureHttpClientUser httpUser, AzureHttpClientLegacy azureHttpClientLegacy) : base(configuration)
        {
            _logger = logger;
            _httpConfigured = azureHttpClientConfigured;
            _httpUser = httpUser;
            _httpUserLegacy = azureHttpClientLegacy;
        }
        public async Task<bool> PatProof(string pat)
        {
            try
            {
                var ctx = Ctx(ProjectType);
                var url = $"{ctx.BaseUrl}/_apis/git/repositories?api-version=7.1-preview.1&$top=1";
                var response = await _httpUser.GetAsync(url, pat);
                var isValid = response.StatusCode == HttpStatusCode.OK;

                _logger.LogInformation("PAT validation result: {IsValid}", isValid);
                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating PAT");
                return false;
            }
        }
    }
}
