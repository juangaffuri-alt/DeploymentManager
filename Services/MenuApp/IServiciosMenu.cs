using PasadorScript.DTO;
using PasadorScript.DTO.Sugus;

namespace PasadorScript.Services.MenuApp
{
    public interface IServiciosMenu
    {
        Task<ICollection<Menu>> GetMenu(Aplicativo aplicativo);
        Task<ICollection<Menu>> GetMenu();


    }
}