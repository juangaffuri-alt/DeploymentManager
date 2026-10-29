
namespace PasadorScript.Services.Pat
{
    public interface IPatRepositoryService
    {
        Task<string> GetPatByUser(string username);
    }
}