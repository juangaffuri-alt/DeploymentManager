using Mapster;
using PasadorScript.DbModels;
using PasadorScript.DTO.AzureDev;
using PasadorScript.ViewModels;

namespace PasadorScript.Helpers
{
    public class MappingRegister : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<BuildsGx, BuildGXViewModel>()
                .Map(dest => dest.DescripcionTipoCambio, src => src.IdTipoCambioNavigation.Descripcion)
                .Map(dest => dest.BtRepo, src => src.RepositoryId);

            config.NewConfig<Script, ScriptViewModel>()
                .Map(dest => dest.TipoCambioDescripcion, src => src.IdTipoCambioNavigation.Descripcion);

            // Unificar configuración para EnviosDlya -> EnvioDlyaViewModel
            config.NewConfig<EnviosDlya, EnvioDlyaViewModel>()
                .Map(dest => dest.DescripcionTipoCambio, src => src.IdTipoCambioNavigation.Descripcion)
                .Ignore(dest => dest.CommitListJson);  // Ignora las propiedades pesadas

            config.NewConfig<AzureDevopsPullRequest, PullRequestForceManualViewModel>();
        }
    }
}
