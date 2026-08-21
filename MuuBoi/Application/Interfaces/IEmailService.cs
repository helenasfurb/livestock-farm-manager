namespace MuuBoi.Application.Interfaces
{
    public interface IEmailService
    {
        Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetToken);
    }
}
