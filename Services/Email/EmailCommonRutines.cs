using PasadorScript.Models;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;

namespace PasadorScript.Services.Email
{
    public abstract class EmailCommonRutines
    {
        protected static string ReplaceTemplatePlaceholders(string template, Dictionary<string, string> replacements)
        {
            var encoded = new Dictionary<string, string>(replacements.Count);
            foreach (var replacement in replacements)
            {
                var raw = replacement.Value ?? string.Empty;
                string safe;
                if (replacement.Key is "SourceBranch" or "TargetBranch" or "Repository")
                    safe = EncodeWithWrapHints(raw, '/', '-');
                else if (replacement.Key == "PullRequestUrlText")
                    safe = EncodeWithWrapHints(raw, '/', '?', '&', '=', '-');
                else
                {
                    safe = WebUtility.HtmlEncode(raw);
                    if (replacement.Key == "Description")
                        safe = safe.Replace("\r\n", "<br>").Replace("\n", "<br>");
                }
                encoded[replacement.Key] = safe;
            }

            return Regex.Replace(
                template,
                @"\{\{([A-Za-z0-9_]+)\}\}",
                match => encoded.TryGetValue(match.Groups[1].Value, out var value) ? value : match.Value);
        }

        private static string EncodeWithWrapHints(string value, params char[] separators)
        {
            const string mark = "%%WBR%%";
            foreach (var separator in separators)
                value = value.Replace(separator.ToString(), separator + mark);
            return WebUtility.HtmlEncode(value).Replace(mark, "<wbr>");
        }

        protected async Task SendEmailAsync(EmailSettings _settings, string toEmail, string subject, string body, string CC = "")
        {
            using var smtpClient = new SmtpClient(_settings.SmtpServer)
            {
                Port = _settings.SmtpPort,
                Credentials = new NetworkCredential(_settings.Username, _settings.Password),
                EnableSsl = _settings.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(_settings.FromEmail, _settings.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8,
                
            };

            // Agregar destinatarios
            foreach (var email in toEmail.Split(';', ','))
            {
                if (!string.IsNullOrWhiteSpace(email))
                    mailMessage.To.Add(email.Trim());
            }
            // copias
            if (!String.IsNullOrEmpty(CC))
            {
                foreach (var email in CC.Split(';', ','))
                {
                    if (!string.IsNullOrWhiteSpace(email))
                        mailMessage.CC.Add(email.Trim());
                }
            }

            // Envia
            await smtpClient.SendMailAsync(mailMessage);
        }
    }
}
