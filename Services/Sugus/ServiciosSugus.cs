using OpenIddict.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PasadorScript.DbModels.Constants;
using PasadorScript.DTO;
using PasadorScript.DTO.AzureDev;
using PasadorScript.DTO.Sugus;
using PasadorScript.Helpers;
using PasadorScript.Repository;
using PasadorScript.Services.Token;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Aplicativo = PasadorScript.DTO.Sugus.Aplicativo;


namespace PasadorScript.Services.Sugus
{
    public class ServiciosSugus : IServiciosSugus
    {
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _provider;
        private readonly ITokenService _tokenService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly TempDataHelper _tempDataHelper;
        private readonly HttpClient _httpClient;
        private readonly ILogger<ServiciosSugus> _logger;

        public ServiciosSugus(IConfiguration configuration,
            IServiceProvider provider, ITokenService tokenService,
            IHttpContextAccessor httpContextAccessor, TempDataHelper tempDataHelper,
            ILogger<ServiciosSugus> logger)
        {
            _configuration = configuration;
            _provider = provider;
            _tokenService = tokenService;
            _httpContextAccessor = httpContextAccessor;
            _tempDataHelper = tempDataHelper;
            _logger = logger;

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
            _httpClient = new HttpClient(handler);
        }

        private UserJwt GetLoggedUserFromClaims(string access_token)
        {
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(access_token);
            var usr = new UserJwt();
            if (token != null)
            {
                usr.Email = token.Claims.FirstOrDefault(c => c.Type == OpenIddictConstants.Claims.Email)?.Value!;
                usr.FamilyName = token.Claims.FirstOrDefault(c => c.Type == OpenIddictConstants.Claims.FamilyName)?.Value!;
                usr.Username = token.Claims.FirstOrDefault(c => c.Type == OpenIddictConstants.Claims.Subject)?.Value!;
            }
            return usr;
        }

        public async Task<Aplicativo> LoginApp()
        {
            var appId = int.Parse(_configuration["SugusApi:IdApp"]!);
            var apiUrl = $"{_configuration["SugusApi:BaseAddress"]}/LoginApp/{appId}";

            var token = await _tokenService.GetValidAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                throw new UnauthorizedAccessException("No se pudo obtener un token válido");
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            try
            {
                var response = await _httpClient.PostAsync(apiUrl, null);
                response.EnsureSuccessStatusCode();

                var aplicativo = await response.Content.ReadFromJsonAsync<Aplicativo>();
                if (aplicativo == null)
                    throw new Exception("Aplicativo inválido");

                aplicativo.LoggedUser = GetLoggedUserFromClaims(token);

                // Guardar perfil activo por sesión
                if (aplicativo.Perfiles?.Any() == true)
                {
                    var perfilActivo = aplicativo.Perfiles.First().Id;
                    var descPerfilActivo = aplicativo.Perfiles.First().Descripcion;
                    aplicativo.PerfilActivo = perfilActivo;
                    aplicativo.PerfilActivoDescripcion = descPerfilActivo;

                    _httpContextAccessor.HttpContext!.Session.SetInt32("PerfilActivo", perfilActivo);
                    _httpContextAccessor.HttpContext!.Session.SetString("DescPerfilActivo", descPerfilActivo);
                }
                else
                {
                    throw new Exception("Usuario sin perfiles asignados");
                }

                return aplicativo;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error HTTP al contactar SugusApi");
                throw new Exception($"Error de conectividad con SugusApi: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado en LoginApp");
                throw;
            }
        }

        public async Task<Aplicativo?> GetInfoApp()
        {
            var appId = int.Parse(_configuration["SugusApi:IdApp"]!);
            var apiUrl = $"{_configuration["SugusApi:BaseAddress"]}/UserInApp/{appId}";

            var token = await _tokenService.GetValidAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Token inválido o expirado en GetInfoApp");
                return null;
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            try
            {
                var response = await _httpClient.PostAsync(apiUrl, null);
                response.EnsureSuccessStatusCode();

                var aplicativo = await response.Content.ReadFromJsonAsync<Aplicativo>();
                if (aplicativo == null)
                {
                    _logger.LogWarning("Respuesta nula de SugusApi");
                    return null;
                }

                aplicativo.LoggedUser = GetLoggedUserFromClaims(token);

                // Recuperar perfil activo de la sesión o usar el primero
                var perfilActivo = _httpContextAccessor.HttpContext!.Session.GetInt32("PerfilActivo");
                var descPerfilActivo = _httpContextAccessor.HttpContext!.Session.GetString("DescPerfilActivo");

                if (perfilActivo.HasValue && aplicativo.Perfiles?.Any(p => p.Id == perfilActivo.Value) == true)
                {
                    aplicativo.PerfilActivo = perfilActivo.Value;
                    aplicativo.PerfilActivoDescripcion = descPerfilActivo!;
                }
                else if (aplicativo.Perfiles?.Any() == true)
                {
                    // Si no hay perfil en sesión o no es válido, usar el primero
                    var primerPerfil = aplicativo.Perfiles.First();
                    aplicativo.PerfilActivo = primerPerfil.Id;
                    aplicativo.PerfilActivoDescripcion = primerPerfil.Descripcion;

                    _httpContextAccessor.HttpContext!.Session.SetInt32("PerfilActivo", primerPerfil.Id);
                    _httpContextAccessor.HttpContext!.Session.SetString("DescPerfilActivo", primerPerfil.Descripcion);
                }
                else
                {
                    _logger.LogWarning("Usuario sin perfiles asignados");
                    return null;
                }

                return aplicativo;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error HTTP al contactar SugusApi en GetInfoApp");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado en GetInfoApp");
                return null;
            }
        }

        public async Task<bool> LogOutApp()
        {
            var loginAppRequest = int.Parse(_configuration["SugusApi:IdApp"]!);
            try
            {
                return await CallToLogout(loginAppRequest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en LogOutApp");
                // Aunque falle el logout en el servidor, limpiamos la sesión local
                return false;
            }
        }

        protected async Task<bool> CallToLogout(int idApp)
        {
            var token = await _tokenService.GetValidAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("No hay token válido para logout");
                return false;
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var apiUrl = $"{_configuration["SugusApi:BaseAddress"]}/LogoutApp?idAplicacion={idApp}";

            try
            {
                var response = await _httpClient.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<bool>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CallToLogout");
                return false;
            }
        }

        public async Task ChangePerfilActivo(int newRol, string descripcion)
        {
            _httpContextAccessor.HttpContext!.Session.SetInt32("PerfilActivo", newRol);
            _httpContextAccessor.HttpContext!.Session.SetString("DescPerfilActivo", descripcion);
        }
    }
}
