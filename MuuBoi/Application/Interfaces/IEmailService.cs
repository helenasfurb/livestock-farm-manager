namespace MuuBoi.Application.Interfaces
{
    public interface IEmailService
    {
        Task<bool> SendPasswordResetCodeAsync(string toEmail, string toName, string code);
        Task<bool> SendEmailConfirmationCodeAsync(string toEmail, string toName, string code);
    }
}
