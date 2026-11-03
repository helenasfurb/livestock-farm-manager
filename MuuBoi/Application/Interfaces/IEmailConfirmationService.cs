using Microsoft.AspNetCore.Identity;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Interfaces
{
    public interface IEmailConfirmationService
    {
        Task<bool> SendCodeAsync(ApplicationUser user);
        Task<bool> ResendAsync(string email);
        Task<IdentityResult> ConfirmAsync(string email, string code);
    }
}
