using Microsoft.AspNetCore.Identity;

namespace MuuBoi.Application.Interfaces
{
    public interface IPasswordRecoveryService
    {
        Task<bool> RequestResetAsync(string email);
        Task<IdentityResult> ResetPasswordAsync(string email, string code, string newPassword);
    }
}
