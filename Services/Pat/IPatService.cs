namespace PasadorScript.Services.Pat
{
    public interface IPatService
    {
        Task<bool> UserHasValidPatAsync(string username);
    }
}
