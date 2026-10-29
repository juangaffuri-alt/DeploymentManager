using PasadorScript.DTO.TeamDev;

namespace PasadorScript.Services.TeamDev
{
    public interface IServiciosTeamDev
    {
        Task<ICollection<CommitsTeamDev>> GetCommitsFromServer(LogQueryParameters parameters);
        Task<PaginatedResult<CommitsTeamDev>> GetCommitsFromServerPaginated(LogQueryParameters parameters);
    }
}