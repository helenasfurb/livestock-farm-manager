using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using MuuBoi.Application.Helpers;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Services
{
    public class EmailConfirmationService : IEmailConfirmationService
    {
        private static readonly TimeSpan SendCooldown = TimeSpan.FromSeconds(60);
        private static readonly IdentityError InvalidCodeError = new()
        {
            Code = nameof(IdentityErrorDescriber.InvalidToken),
            Description = "Código inválido ou expirado."
        };

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;
        private readonly IMemoryCache _cache;

        public EmailConfirmationService(UserManager<ApplicationUser> userManager, IEmailService emailService, IMemoryCache cache)
        {
            _userManager = userManager;
            _emailService = emailService;
            _cache = cache;
        }

        public async Task<bool> SendCodeAsync(ApplicationUser user)
        {
            var cooldownKey = $"email-confirmation-sent:{user.Id}";
            if (_cache.TryGetValue(cooldownKey, out _))
                return false;

            if (string.IsNullOrEmpty(user.SecurityStamp))
                await _userManager.UpdateSecurityStampAsync(user);

            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var sent = await _emailService.SendEmailConfirmationCodeAsync(user.Email!, user.Name, code);
            if (sent)
                _cache.Set(cooldownKey, true, SendCooldown);

            return sent;
        }

        public async Task<bool> ResendAsync(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null || !user.IsActive || user.EmailConfirmed)
                return false;

            return await SendCodeAsync(user);
        }

        public async Task<IdentityResult> ConfirmAsync(string email, string code)
        {
            var attemptsKey = CodeAttemptLimiter.KeyFor("email-confirmation", email);
            if (CodeAttemptLimiter.IsLocked(_cache, attemptsKey))
                return IdentityResult.Failed(InvalidCodeError);

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null || !user.IsActive)
                return RegisterFailure(attemptsKey);

            if (user.EmailConfirmed)
            {
                var stillValid = await _userManager.VerifyUserTokenAsync(
                    user,
                    _userManager.Options.Tokens.EmailConfirmationTokenProvider,
                    UserManager<ApplicationUser>.ConfirmEmailTokenPurpose,
                    code);
                return stillValid ? IdentityResult.Success : RegisterFailure(attemptsKey);
            }

            var result = await _userManager.ConfirmEmailAsync(user, code);
            if (!result.Succeeded)
                return RegisterFailure(attemptsKey);

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
