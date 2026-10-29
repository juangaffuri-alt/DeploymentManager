using PasadorScript.DTO.Sugus;
using PasadorScript.Services.AdGroups;

namespace PasadorScript.Services.Sugus
{
    public class ServiciosSugusMok : IServiciosSugus
    {
        private readonly IAdGroupService _adGroupService;
        private readonly IConfiguration _configuration;

        public ServiciosSugusMok(IAdGroupService adGroupService, IConfiguration configuration)
        {
            _adGroupService = adGroupService;
            _configuration = configuration;
        }
        public Task ChangePerfilActivo(int newRol, string descripcion)
        {
            throw new NotImplementedException();
        }

        public async Task<Aplicativo?> GetInfoApp()
        {
            var usr = await _adGroupService.GetInfoUser();
            var r = new Aplicativo()
            {
                Descripcion = "P230",
                Id = 1015,
                LoggedUser = new DTO.UserJwt()
                {
                    Username = usr.Username,
                    Email = usr.Email,
                    FamilyName = usr.FamilyName
                },
                PerfilActivo = 1,
                PerfilActivoDescripcion = "Analista Técnico",
                Perfiles = new List<Perfil>()
                {
                    new Perfil()
                    {
                        Id = 1,
                        Descripcion = "Analista técnico"

                    }
                }
            };
            return r;
        }

        public Task<bool> LogOutApp()
        {
            throw new NotImplementedException();
        }
    }
}
