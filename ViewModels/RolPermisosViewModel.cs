namespace PasadorScript.ViewModels
{
    public class RolPermisosViewModel
    {
        public string GroupGuid { get; set; }
        public string GroupName { get; set; }
        public string Description { get; set; }
        public string Email { get; set; }
        public List<AccionViewModel> AccionesPermitidas { get; set; }
    }
}
