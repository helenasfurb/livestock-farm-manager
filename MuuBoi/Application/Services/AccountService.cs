using MuuBoi.Application.Interfaces;

namespace MuuBoi.Application.Services
{
    public class AccountService : IAccountService
    {
        private readonly IAccountRepository _accountRepository;
        private readonly ISessionValidator _sessionValidator;

        public AccountService(IAccountRepository accountRepository, ISessionValidator sessionValidator)
        {
            _accountRepository = accountRepository;
            _sessionValidator = sessionValidator;
        }

        public async Task<bool> DeletePropertyAsync(Guid propertyId)
        {
            var userIds = await _accountRepository.GetUserIdsByPropertyAsync(propertyId);

            await _accountRepository.DeletePropertyAsync(propertyId);

            foreach (var userId in userIds)
                _sessionValidator.Invalidate(userId);

            return true;
        }
    }
}
