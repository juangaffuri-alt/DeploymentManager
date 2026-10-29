
using System.IdentityModel.Tokens.Jwt;

namespace PasadorScript.Services.Token
{
    public class TestingTokenServiceDecorator : TokenService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TokenService> _logger;
        public TestingTokenServiceDecorator(IHttpContextAccessor accessor, IConfiguration config, ILogger<TokenService> logger) : base(accessor, config, logger)
        {
            _httpContextAccessor = accessor;
            _configuration = config;
            _logger = logger;
        }
        public override bool IsTokenValid(string accessToken)
        {
            // Si está habilitado el modo testing
            if (_configuration.GetValue<bool>("Testing:UseCustomTokenExpiry"))
            {
                try
                {
                    var handler = new JwtSecurityTokenHandler();
                    var token = handler.ReadJwtToken(accessToken);

                    // Obtener configuración personalizada
                    var customLifetimeMinutes = _configuration.GetValue("Testing:TokenLifetimeMinutes", 2);

                    // Calcular expiración personalizada basada en iat (issued at)
                    var iatClaim = token.Claims.FirstOrDefault(c => c.Type == "iat");
                    if (iatClaim != null && long.TryParse(iatClaim.Value, out var iatValue))
                    {
                        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(iatValue);
                        var customExpiry = issuedAt.AddMinutes(customLifetimeMinutes);

                        //var expirationBuffer = TimeSpan.FromMinutes(5);
                        //var isValid = DateTime.UtcNow.Add(expirationBuffer) < customExpiry;
                        var isValid = DateTime.UtcNow < customExpiry;
                        return isValid;
                    }
                }
                catch (Exception ex)
                {

                }
            }
            return false;

        }

    }
}
