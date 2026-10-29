namespace PasadorScript.DTO.Sugus
{
    public class Aplicativo
    {
        public int Id { get; set; }
        public string Descripcion { get; set; }
        public UserJwt LoggedUser { get; set; } = new UserJwt();
        public int PerfilActivo { get; set; } = 0;
        public string PerfilActivoDescripcion { get; set; }
        public List<Perfil> Perfiles { get; set; } = new List<Perfil>();
        public string UserDescriptor { get; set; }

    }
}
