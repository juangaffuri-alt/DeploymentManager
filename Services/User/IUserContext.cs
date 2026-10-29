namespace PasadorScript.Services.User
{
    public interface IUserContext
    {
        string PersonalAccessToken { get; }
        string UserId { get; }
    }
}