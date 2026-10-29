namespace PasadorScript.DTO
{
    public class Menu
    {
        public int IdMenu { get; set; }
        public string Descripcion { get; set; }
        public string AspController { get; set; }
        public string AspAction { get; set; }
        public string IClass { get; set; }
        public ICollection<Submenu> Submenues { get; set; }
        public Menu()
        {
            Submenues = new List<Submenu>();
        }
    }
}
