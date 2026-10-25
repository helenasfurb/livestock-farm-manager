using Microsoft.Extensions.Logging;
using MuuBoi.Application.Helpers;
using MuuBoi.Application.Interfaces;

namespace MuuBoi.Application.Services
{
    public class AccountService : IAccountService
    {
        private readonly IAccountRepository _accountRepository;
        private readonly ISessionValidator _sessionValidator;
        private readonly IFileStorageService _fileStorage;
        private readonly ILogger<AccountService> _logger;

        public AccountService(
            IAccountRepository accountRepository,
            ISessionValidator sessionValidator,
            IFileStorageService fileStorage,
            ILogger<AccountService> logger)
        {
            _accountRepository = accountRepository;
            _sessionValidator = sessionValidator;
            _fileStorage = fileStorage;
            _logger = logger;
        }

        public async Task<bool> DeletePropertyAsync(Guid propertyId)
        {
            var userIds = await _accountRepository.GetUserIdsByPropertyAsync(propertyId);

            await _accountRepository.DeletePropertyAsync(propertyId);

            foreach (var userId in userIds)
                _sessionValidator.Invalidate(userId);

            await DeletePropertyFilesAsync(propertyId);

            return true;
        }

        private async Task<bool> DeletePropertyFilesAsync(Guid propertyId)
        {
            try
            {
                await _fileStorage.DeleteByPrefixAsync(AnimalPhotoPaths.PropertyPrefix(propertyId));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete stored files of property {PropertyId}", propertyId);
                return false;
            }
        }
    }
}
