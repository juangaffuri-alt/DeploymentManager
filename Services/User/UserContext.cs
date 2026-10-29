namespace PasadorScript.Services.User
{
    public class UserContext : IUserContext
    {
        public string PersonalAccessToken { get; set; }
        public string UserId { get; set; }
    }
}
