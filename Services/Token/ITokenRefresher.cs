using PasadorScript.DTO;

namespace PasadorScript.Services.Token
{
    public interface ITokenRefresher
    {
        Task<TokenResponse> RefreshAsync();
    }
}