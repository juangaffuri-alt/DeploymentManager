using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authentication;
using System.Globalization;
using System.Text.Json;
using PasadorScript.DTO;
using System.Net;

namespace PasadorScript.Services.Token
{
    public class ServiciosTokenRefresh : ITokenRefresher
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;


        public ServiciosTokenRefresh(
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration)
        {
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
        }

        public async Task<TokenResponse> RefreshAsync()
        {
            var httpContext = _httpContextAccessor.HttpContext!;
            // 1. Obtengo el refresh token actual
            var refreshToken = await httpContext.GetTokenAsync(OpenIdConnectDefaults.AuthenticationScheme, "refresh_token");
            if (string.IsNullOrEmpty(refreshToken))
                throw new InvalidOperationException("No se encontró refresh_token.");

            // 2. Preparo la petición al endpoint de token
            var tokenEndpoint = $"{_configuration["OauthConfig:Authority"]}connect/token"; // ej. https://tu-idp.com/connect/token
            var clientId = _configuration["OauthConfig:ClientId"];
            var clientSecret = _configuration["OauthConfig:ClientSecret"];

            var parameters = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
            };

            var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
            {
                Content = new FormUrlEncodedContent(parameters)
            };
            HttpResponseMessage rm = null;
            try
            {
                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                };

                var _httpClient = new HttpClient(handler);

                rm = await _httpClient.SendAsync(request);
                if (!rm.IsSuccessStatusCode)
                    throw new ApplicationException($"Error refrescando token: {rm.StatusCode}");

                var payload = JsonSerializer.Deserialize<JsonElement>(
                    await rm.Content.ReadAsStringAsync());

                // 3. Extraigo los nuevos tokens
                var newAccessToken = payload.GetProperty("access_token").GetString()!;
                var newRefreshToken = payload.GetProperty("refresh_token").GetString()!;
                var expiresIn = payload.GetProperty("expires_in").GetInt32();

                // 4. Actualizo la cookie de autenticación con los nuevos tokens
                var info = await httpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                info.Properties.UpdateTokenValue("access_token", newAccessToken);
                info.Properties.UpdateTokenValue("refresh_token", newRefreshToken);
                info.Properties.UpdateTokenValue("expires_at",
                    DateTime.UtcNow.AddSeconds(expiresIn)
                            .ToString("o", CultureInfo.InvariantCulture));

                await httpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    info.Principal!,
                    info.Properties);

                return new TokenResponse
                {
                    AccessToken = newAccessToken,
                    RefreshToken = newRefreshToken,
                    ExpiresIn = expiresIn
                };
            }
            catch (WebException ex)
            {
                var payload = JsonSerializer.Deserialize<JsonElement>(
                    await rm.Content.ReadAsStringAsync());
                throw new Exception(payload.ToString());
            }
        }
    }
}
