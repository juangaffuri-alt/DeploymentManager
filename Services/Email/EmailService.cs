using Microsoft.Extensions.Configuration;
using PasadorScript.Models;
using PasadorScript.Services.AdGroups;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace PasadorScript.Services.Email
{
    public class EmailService : EmailCommonRutines, IEmailService
    {
        private readonly EmailSettings _settings;
        private readonly IAdGroupService _adGroupService;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public EmailService(IConfiguration configuration, IWebHostEnvironment environment, IAdGroupService adGroupService)
        {
            _adGroupService = adGroupService;
            _configuration = configuration;
            _settings = _configuration.GetSection("EmailSettings").Get<EmailSettings>()!;
            _environment = environment;
        }

        public Task SendPullRequestNotificationDBAAsync(
            string areaName,
            string toEmail,
            string pullRequestId,
            string pullRequestTitle,
            string repository,
            string pullRequestUrl,
            Dictionary<string, string>? additionalData = null)
        {
            return SendRevisionPendienteAsync(
                areaName,
                toEmail,
                pullRequestId,
                pullRequestTitle,
                repository,
                pullRequestUrl,
                "Script SQL",
                additionalData);
        }

        public Task SendPullRequestNotificationIYCAsync(
            string areaName,
            string toEmail,
            string pullRequestId,
            string pullRequestTitle,
            string repository,
            string pullRequestUrl,
            Dictionary<string, string>? additionalData = null)
        {
            return SendRevisionPendienteAsync(
                areaName,
                toEmail,
                pullRequestId,
                pullRequestTitle,
                repository,
                pullRequestUrl,
                Texto(additionalData, "TipoSolicitud", "Build GeneXus"),
                additionalData);
        }

        private async Task SendRevisionPendienteAsync(
            string areaName,
            string toEmail,
            string pullRequestId,
            string pullRequestTitle,
            string repository,
            string pullRequestUrl,
            string tipoSolicitud,
            Dictionary<string, string>? additionalData)
        {
            try
            {
                var templatePath = Path.Combine(_environment.WebRootPath, "Templates", "RevisionPendiente.html");
                var templateContent = await File.ReadAllTextAsync(templatePath);
                var me = await _adGroupService.GetInfoUser();
                var createdBy = Texto(additionalData, "CreatedBy", "No especificado");
                var title = string.IsNullOrWhiteSpace(pullRequestTitle) ? "Sin título" : pullRequestTitle;
                var intro = $"Hay un pasaje esperando la aprobación de {areaName}. El enlace abre la pantalla para aprobar o rechazar.";

                var emailBody = ReplaceTemplatePlaceholders(templateContent, new Dictionary<string, string>
                {
                    { "Preheader", $"{tipoSolicitud} · {title} · {createdBy}" },
                    { "TipoSolicitud", tipoSolicitud },
                    { "Intro", intro },
                    { "PullRequestId", pullRequestId },
                    { "PullRequestTitle", title },
                    { "CreatedBy", createdBy },
                    { "CreationDate", Texto(additionalData, "CreationDate", DateTime.Now.ToString("dd/MM/yyyy HH:mm")) },
                    { "Repository", string.IsNullOrWhiteSpace(repository) ? "No informado" : repository },
                    { "Description", Texto(additionalData, "Description", "Sin descripción") },
                    { "PullRequestUrl", pullRequestUrl ?? string.Empty },
                    { "PullRequestUrlText", pullRequestUrl ?? string.Empty },
                    { "CurrentDate", DateTime.Now.ToString("dd/MM/yyyy HH:mm") }
                });

                await SendEmailAsync(
                    _settings,
                    toEmail,
                    $"[{areaName}] Aprobación pendiente · PR {pullRequestId} · {pullRequestTitle}",
                    emailBody,
                    me.Email);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error enviando email: {ex.Message}");
                throw;
            }
        }

        private static string Texto(Dictionary<string, string>? data, string key, string fallback)
        {
            if (data != null && data.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;
            return fallback;
        }
    }


}
