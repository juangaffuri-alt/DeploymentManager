using PasadorScript.DbModels.Constants;
using PasadorScript.DTO.Sugus;

namespace PasadorScript.Services.Sugus
{
    public interface IServiciosSugus
    {
        Task<Aplicativo?> GetInfoApp();
        Task<bool> LogOutApp();
        Task ChangePerfilActivo(int newRol, string descripcion);

    }
}