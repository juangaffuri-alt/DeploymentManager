using PasadorScript.DTO.Graph;

namespace PasadorScript.Services.Graph
{
    public interface IServiciosGraph
    {
        Task<ICollection<string>> GroupsByUser(string descriptorUser);
        Task<UserInfo?> InfoUser(string directoryAlias);
        Task<UserInfo?> InfoUserByEmail(string email);
        Task<UserInfo?> InfoUserByDisplayName(string displayName);
        Task<GraphUserResponse> GetMyIdAsAsync();
        Task<GraphUserResponse> GetMyIdAsAsync(string pat);
        Task<GraphUserResponse> GetUserByAdName(string pat, string activeDirectoryName);
        Task<bool> UsuarioPerteneceAGrupoAsync(string directoryAlias, string groupDescriptor);
    }
}