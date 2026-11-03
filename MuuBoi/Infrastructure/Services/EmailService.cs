using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MuuBoi.Application.Interfaces;
using MuuBoi.Application.Options;

namespace MuuBoi.Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailOptions _options;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<EmailOptions> options, ILogger<EmailService> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public Task<bool> SendPasswordResetCodeAsync(string toEmail, string toName, string code)
        {
            var body = $"""
                Olá, {toName}!

                Seu código para redefinir a senha do MuuBoi é:

                    {code}

                Ele é válido por alguns minutos. Se você não pediu a redefinição, ignore este e-mail — sua senha continua a mesma.

                Equipe MuuBoi
                """;
            return SendAsync(toEmail, toName, "Código para redefinir sua senha — MuuBoi", body);
        }

        public Task<bool> SendEmailConfirmationCodeAsync(string toEmail, string toName, string code)
        {
            var body = $"""
                Olá, {toName}!

                Seu código para confirmar o e-mail no MuuBoi é:

                    {code}

                Ele é válido por alguns minutos. Se você não criou uma conta no MuuBoi, ignore este e-mail.

                Equipe MuuBoi
                """;
            return SendAsync(toEmail, toName, "Confirme seu e-mail — MuuBoi", body);
        }

        private async Task<bool> SendAsync(string toEmail, string toName, string subject, string body)
        {
            try
            {
                using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(_options.Username, _options.Password)
                };

                using var message = new MailMessage
                {
                    From = new MailAddress(_options.FromEmail, _options.FromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = false
                };
                message.To.Add(new MailAddress(toEmail, toName));

                await client.SendMailAsync(message);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email '{Subject}' to {Email}", subject, toEmail);
                return false;
            }
        }
    }
}
