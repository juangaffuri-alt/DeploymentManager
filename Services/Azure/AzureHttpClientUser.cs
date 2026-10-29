using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PasadorScript.Services.AdGroups;
using PasadorScript.Services.Pat;
using PasadorScript.Services.User;

namespace PasadorScript.Services.Azure
{
    /// <summary>
    /// HttpClient que obtiene PAT desde parámetro explícito o IAdGroupService (usuario actual)
    /// Prioridad: Parámetro explícito > Usuario actual
    /// Ciclo de vida: Scoped
    /// </summary>
    public class AzureHttpClientUser : AzureHttpClientBase
    {
        private readonly IUserContext _userContext;

        public AzureHttpClientUser(
            IHttpClientFactory factory,
            ILogger<AzureHttpClientUser> logger, IUserContext userContext)
            : base(factory, logger)
        {
            _userContext = userContext;
        }

        /// <summary>
        /// Resuelve el PAT desde: parámetro explícito > usuario actual
        /// </summary>
        protected override async Task<string> ResolvePat(string pat = null)
        {
            // 1. Prioridad: Parámetro explícito
            if (!string.IsNullOrEmpty(pat))
            {
                Logger.LogDebug("Using explicit PAT parameter");
                return pat;
            }

            // 2. Intentar obtener del usuario actual
            try
            {
                if (!String.IsNullOrEmpty(_userContext.PersonalAccessToken))
                {
                    Logger.LogDebug("Using PAT from IUserContext");
                    return _userContext.PersonalAccessToken;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Could not get user info from IAdGroupService");
            }

            throw new InvalidOperationException(
                "No se pudo resolver PAT. Proporcione uno explícitamente vía parámetro.");
        }
    }
}