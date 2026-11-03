using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Services
{
    public class UsageService : IUsageService
    {
        private static readonly int LoginRetentionMonths = 12;

        private readonly IUsageRepository _usageRepository;
        private readonly IUsageTracker _usageTracker;
        private readonly TimeProvider _timeProvider;

        public UsageService(IUsageRepository usageRepository, IUsageTracker usageTracker, TimeProvider timeProvider)
        {
            _usageRepository = usageRepository;
            _usageTracker = usageTracker;
            _timeProvider = timeProvider;
        }

        public async Task<bool> RecordLoginAsync(string userId, Guid propertyId)
        {
            await _usageRepository.AddLoginEventAsync(new LoginEvent
            {
                UserId = userId,
                PropertyId = propertyId,
                OccurredAt = _timeProvider.GetUtcNow().UtcDateTime
            });

            _usageTracker.RecordLogin(propertyId);

            return true;
        }

        public async Task<bool> FlushAsync()
        {
            var counts = _usageTracker.Drain();

            for (var i = 0; i < counts.Count; i++)
            {
                try
                {
                    await _usageRepository.AddUsageAsync(counts[i]);
                }
                catch
                {
                    _usageTracker.Restore(counts.Skip(i));
                    throw;
                }
            }

            return true;
        }

        public async Task<bool> PurgeLoginEventsAsync()
        {
            var cutoff = _timeProvider.GetUtcNow().UtcDateTime.AddMonths(-LoginRetentionMonths);

            await _usageRepository.DeleteLoginEventsBeforeAsync(cutoff);

            return true;
        }
    }
}
