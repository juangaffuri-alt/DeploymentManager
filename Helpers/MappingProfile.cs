/*using AutoMapper;
using Elfie.Serialization;
using PasadorScript.DbModels;
using PasadorScript.DTO.AzureDev;
using PasadorScript.ViewModels;

namespace PasadorScript.Helpers
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<BuildsGx, BuildGXViewModel>()
                .ForMember(dest => dest.DescripcionTipoCambio, opt => opt.MapFrom(src => src.IdTipoCambioNavigation.Descripcion))
                .ForMember(dest => dest.BtRepo, opt => opt.MapFrom(src => src.RepositoryId)); 
            CreateMap<Script, ScriptViewModel>()
                .ForMember(dest=> dest.TipoCambioDescripcion, opt=> opt.MapFrom(src => src.IdTipoCambioNavigation.Descripcion));
            CreateMap<EnviosDlya, EnvioDlyaViewModel>()
                .ForMember(dest => dest.DescripcionTipoCambio, opt => opt.MapFrom(src => src.IdTipoCambioNavigation.Descripcion));
            CreateMap<AzureDevopsPullRequest, PullRequestForceManualViewModel>();
            CreateMap<EnviosDlya, EnvioDlyaViewModel>()
                .ForMember(dest => dest.CommitListJson, opt => opt.Ignore()); // Ignora las propiedades pesadas    

        }
    }
}
*/