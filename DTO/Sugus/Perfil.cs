namespace PasadorScript.DTO.Sugus
{
    public class Perfil
    {
        public int Id { get; set; }
        public string Descripcion { get; set; }
        public ICollection<Permiso> Permisos { get; set; } = new List<Permiso>();

    }
}
