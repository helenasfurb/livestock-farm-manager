using System.Net;
using System.Net.Mail;
using MuuBoi.Application.Interfaces;

namespace MuuBoi.Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetToken)
        {
            var smtpHost = _configuration["Email:SmtpHost"]!;
            var smtpPort = int.Parse(_configuration["Email:SmtpPort"]!);
            var fromEmail = _configuration["Email:FromEmail"]!;
            var fromName = _configuration["Email:FromName"] ?? "MuuBoi";
            var username = _configuration["Email:Username"]!;
            var password = _configuration["Email:Password"]!;
            var frontendUrl = _configuration["Email:FrontendUrl"] ?? "http://localhost:3000";

            var encodedToken = Uri.EscapeDataString(resetToken);
            var encodedEmail = Uri.EscapeDataString(toEmail);
            var resetLink = $"{frontendUrl}/reset-password?token={encodedToken}&email={encodedEmail}";

            var body = $"""
                Olá, {toName}!

                Recebemos uma solicitação para redefinir a senha da sua conta no MuuBoi.

                Clique no link abaixo para criar uma nova senha:
                {resetLink}

                Este link expira em 1 hora.

                Se você não solicitou a redefinição de senha, ignore este email.

                Atenciosamente,
                Equipe MuuBoi
                """;

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(username, password)
            };

            var message = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = "Redefinição de senha — MuuBoi",
                Body = body,
                IsBodyHtml = false
            };
            message.To.Add(new MailAddress(toEmail, toName));

            await client.SendMailAsync(message);
        }
    }
}
