using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Mimsv2.Models;

namespace Mimsv2.Services
{
    public class EmailService
    {
        private readonly SmtpSettings _settings;

        public EmailService(IOptions<SmtpSettings> options)
        {
            _settings = options.Value;
        }

        public async Task SendPasswordResetEmail(string toEmail, string resetUrl)
        {
            var fromAddress = new MailAddress(_settings.FromEmail, _settings.FromName);
            var toAddress = new MailAddress(toEmail);

            var subject = "Reset Your Password";
            var body = $@"
            <p>Hello,</p>
            <p>You requested a password reset. Click the link below to reset your password:</p>
            <p><a href='{resetUrl}'>{resetUrl}</a></p>
            <p>If you didn’t request this, please ignore this email.</p>";

            var smtp = new SmtpClient
            {
                Host = _settings.Host,
                Port = _settings.Port,
                EnableSsl = _settings.EnableSsl,
                Credentials = new NetworkCredential(_settings.Username, _settings.Password)
            };

            using var message = new MailMessage(fromAddress, toAddress)
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            await smtp.SendMailAsync(message);
        }


        //send incident email, in formcoontroller find the recipients
        public async Task SendIncidentNotificationEmail(string toEmail, string subject, string body, List<string>? cc = null, List<string>? bcc = null)
        {
            var fromAddress = new MailAddress(_settings.FromEmail, _settings.FromName);
            var toAddress = new MailAddress(toEmail);

            var smtp = new SmtpClient
            {
                Host = _settings.Host,
                Port = _settings.Port,
                EnableSsl = _settings.EnableSsl,
                Credentials = new NetworkCredential(_settings.Username, _settings.Password)
            };

            using var message = new MailMessage
            {
                From = fromAddress,
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            message.To.Add(toAddress);

            if (cc != null)
            {
                foreach (var ccEmail in cc)
                {
                    message.CC.Add(ccEmail);
                }
            }

            if (bcc != null)
            {
                foreach (var bccEmail in bcc)
                {
                    message.Bcc.Add(bccEmail);
                }
            }

            await smtp.SendMailAsync(message);
        }



    }
}
