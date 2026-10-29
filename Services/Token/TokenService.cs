using IdentityModel.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.IdentityModel.Tokens.Jwt;


namespace PasadorScript.Services.Token
{
    public class TokenService : ITokenService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TokenService> _logger;

        public TokenService(IHttpContextAccessor accessor, IConfiguration config, ILogger<TokenService> logger)
        {
            _httpContextAccessor = accessor;
            _configuration = config;
            _logger = logger;
        }

        public async Task<string?> GetValidAccessTokenAsync()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return null;

            var accessToken = await context.GetTokenAsync("access_token");
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("No access token found in context");
                return null;
            }

            // Verificar si el token es válido
            if (!IsTokenValid(accessToken))
            {
                _logger.LogInformation("Access token expired, attempting refresh");
                return await RefreshTokenAsync(context);
            }

            return accessToken;
        }

        public virtual bool IsTokenValid(string accessToken)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var token = handler.ReadJwtToken(accessToken);

                // Agregar un buffer de 5 minutos antes de la expiración para evitar tokens que expiren durante la petición
                var expirationBuffer = TimeSpan.FromMinutes(5);
                return DateTime.UtcNow.Add(expirationBuffer) < token.ValidTo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token");
                return false;
            }
        }

        private async Task<string?> RefreshTokenAsync(HttpContext context)
        {
            var refreshToken = await context.GetTokenAsync("refresh_token");
            if (string.IsNullOrEmpty(refreshToken))
            {
                _logger.LogInformation("No refresh token available");
                return null;
            }

            try
            {
                var disco = await GetDiscoveryDocument();
                if (disco == null)
                {
                    _logger.LogError("Could not get discovery document");
                    return null;
                }

                var tokenClient = new HttpClient();
                var response = await tokenClient.RequestRefreshTokenAsync(new RefreshTokenRequest
                {
                    Address = disco.TokenEndpoint,
                    ClientId = _configuration["OauthConfig:ClientId"],
                    ClientSecret = _configuration["OauthConfig:ClientSecret"],
                    RefreshToken = refreshToken
                });

                if (response.IsError)
                {
                    _logger.LogWarning("Failed to refresh token: {Error} - {ErrorDescription}",
                        response.Error, response.ErrorDescription);

                    // Si el refresh token también expiró, limpiar la sesión
                    await ClearExpiredSession(context);
                    return null;
                }

                // Actualizar los tokens en la sesión
                await UpdateTokensInSession(context, response);

                _logger.LogInformation("Token refreshed successfully");
                return response.AccessToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token refresh");
                await ClearExpiredSession(context);
                return null;
            }
        }

        private async Task UpdateTokensInSession(HttpContext context, TokenResponse tokenResponse)
        {
            var authResult = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (authResult.Succeeded)
            {
                var properties = authResult.Properties;

                // Actualizar tokens
                properties.UpdateTokenValue("access_token", tokenResponse.AccessToken);
                properties.UpdateTokenValue("refresh_token", tokenResponse.RefreshToken);

                // Calcular nueva fecha de expiración
                var expiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                properties.UpdateTokenValue("expires_at", expiresAt.ToString("o"));

                // Sincronizar la expiración de la cookie con el token
                properties.ExpiresUtc = expiresAt;

                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                    authResult.Principal, properties);
            }
        }

        private async Task ClearExpiredSession(HttpContext context)
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            context.Session.Clear();
        }

        private async Task<DiscoveryDocumentResponse?> GetDiscoveryDocument()
        {
            try
            {
                var client = new HttpClient();
                var disco = await client.GetDiscoveryDocumentAsync(_configuration["OauthConfig:Authority"]);
                return disco.IsError ? null : disco;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting discovery document");
                return null;
            }
        }

        public async Task<bool> IsSessionValid()
        {
            var token = await GetValidAccessTokenAsync();
            return !string.IsNullOrEmpty(token);
        }
        public async Task<TimeSpan?> GetTokenRemainingTime()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null)
                return null;

            var expiresAtString = await context.GetTokenAsync("expires_at");
            if (string.IsNullOrEmpty(expiresAtString) || !DateTime.TryParse(expiresAtString, out var expiresAt))
                return null;

            var remaining = expiresAt - DateTime.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        public async Task<bool> ForceTokenRefresh()
        {
            _logger.LogInformation("Forzando renovación de token");

            var context = _httpContextAccessor.HttpContext;
            if (context == null)
            {
                _logger.LogWarning("No hay contexto HTTP disponible para forzar renovación");
                return false;
            }

            var refreshToken = await context.GetTokenAsync("refresh_token");
            if (string.IsNullOrEmpty(refreshToken))
            {
                _logger.LogWarning("No hay refresh token disponible para forzar renovación");
                return false;
            }

            try
            {
                var newAccessToken = await RefreshTokenAsync(context);
                if (!string.IsNullOrEmpty(newAccessToken))
                {
                    _logger.LogInformation("Token renovado exitosamente por forzado");
                    return true;
                }
                else
                {
                    _logger.LogError("Error al forzar renovación del token");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante el forzado de renovación del token");
                return false;
            }
        }
    }
}
