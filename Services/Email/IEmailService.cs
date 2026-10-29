namespace PasadorScript.Services.Email
{
    public interface IEmailService
    {
        Task SendPullRequestNotificationDBAAsync(
            string areaName,
            string toEmail,
            string pullRequestId,
            string pullRequestTitle,
            string repository,
            string pullRequestUrl,
            Dictionary<string, string>? additionalData = null);
        Task SendPullRequestNotificationIYCAsync(
            string areaName,
            string toEmail,
            string pullRequestId,
            string pullRequestTitle,
            string repository,
            string pullRequestUrl,
            Dictionary<string, string>? additionalData = null);
    }
}
