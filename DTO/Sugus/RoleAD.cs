namespace PasadorScript.DTO.Sugus
{
    public class RoleAD
    {
        public int Id { get; set; }

        public string Name { get; set; } = null!;

        public string? Description { get; set; }

        public string? DistinguishedName { get; set; }

        public string? Guid { get; set; }

        public string? Sid { get; set; }

        public string? SamAccountName { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
