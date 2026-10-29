namespace PasadorScript.DTO
{
    public class UserJwt
    {
        public string Username { get; set; } = "";
        public string FamilyName { get; set; } = "";
        public string Email { get; set; } = "";
        public byte[]? Avatar { get; set; }
        public string FullName { get; set; } = "";
    }

    public class AuthRoleAd
    {
        public string Guid { get; set; } = "";
        public string Name { get; set; } = "";
        public string GroupName { get; set; } = "";
    }

    public class AuthUserAd:UserJwt
    {
        public string EmailAzure { get; set; } = "";
        public List<AuthRoleAd> Roles { get; set; } = new();
    }
}
