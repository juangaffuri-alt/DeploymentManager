namespace PasadorScript.Services.Token
{
    public interface ITokenService
    {
        /// <summary>
        /// Obtiene un access token válido, renovándolo si es necesario
        /// </summary>
        /// <returns>Access token válido o null si no se puede obtener</returns>
        Task<string?> GetValidAccessTokenAsync();

        /// <summary>
        /// Verifica si la sesión actual es válida
        /// </summary>
        /// <returns>True si la sesión es válida, false en caso contrario</returns>
        Task<bool> IsSessionValid();

        /// <summary>
        /// Obtiene el tiempo restante antes de que expire el token actual
        /// </summary>
        /// <returns>TimeSpan con el tiempo restante, o null si no hay token</returns>
        Task<TimeSpan?> GetTokenRemainingTime();

        /// <summary>
        /// Fuerza la renovación del token usando el refresh token
        /// </summary>
        /// <returns>True si se pudo renovar, false en caso contrario</returns>
        Task<bool> ForceTokenRefresh();
        bool IsTokenValid(string accessToken);
    }
}