using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using MuuBoi.Application.Helpers;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Services
{
    public class PasswordRecoveryService : IPasswordRecoveryService
    {
        private static readonly IdentityError InvalidCodeError = new()
        {
            Code = nameof(IdentityErrorDescriber.InvalidToken),
            Description = "Código inválido ou expirado."
        };

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;
        private readonly ISessionValidator _sessionValidator;
        private readonly IMemoryCache _cache;

        public PasswordRecoveryService(
            UserManager<ApplicationUser> userManager,
            IEmailService emailService,
            ISessionValidator sessionValidator,
            IMemoryCache cache)
        {
            _userManager = userManager;
            _emailService = emailService;
            _sessionValidator = sessionValidator;
            _cache = cache;
        }

        public async Task<bool> RequestResetAsync(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null || !user.IsActive)
                return false;

            if (string.IsNullOrEmpty(user.SecurityStamp))
                await _userManager.UpdateSecurityStampAsync(user);

            var code = await _userManager.GeneratePasswordResetTokenAsync(user);
            return await _emailService.SendPasswordResetCodeAsync(user.Email!, user.Name, code);
        }

        public async Task<IdentityResult> ResetPasswordAsync(string email, string code, string newPassword)
        {
            var attemptsKey = CodeAttemptLimiter.KeyFor("password-reset", email);
            if (CodeAttemptLimiter.IsLocked(_cache, attemptsKey))
                return IdentityResult.Failed(InvalidCodeError);

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null || !user.IsActive)
                return RegisterFailure(attemptsKey);

            var result = await _userManager.ResetPasswordAsync(user, code, newPassword);
            if (result.Errors.Any(e => e.Code == InvalidCodeError.Code))
                return RegisterFailure(attemptsKey);

            if (!result.Succeeded)
                return result;

            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                await _userManager.UpdateAsync(user);
            }

            _sessionValidator.Invalidate(user.Id);
            CodeAttemptLimiter.Reset(_cache, attemptsKey);
            return result;
        }

        private IdentityResult RegisterFailure(string attemptsKey)
        {
            CodeAttemptLimiter.RegisterFailure(_cache, attemptsKey);
            return IdentityResult.Failed(InvalidCodeError);
        }
    }
}
