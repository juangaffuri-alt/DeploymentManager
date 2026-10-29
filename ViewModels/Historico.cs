namespace PasadorScript.ViewModels
{
    public class Historico
    {
        public int IdEvento { get; set; }
        public DateTime Fecha { get; set; }
        public string? Usuario { get; set; }
        public int IdEstado { get; set; }
        public string? EstadoDescripcion{ get; set; }
        public string? Motivo { get; set; }
        public string? BadgeClass { get; set; }
        public string? Icon { get; set; }
    }
}
