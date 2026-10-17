using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Application.Interfaces;

namespace MuuBoi.Application.Services
{
    public class UsageTracker : IUsageTracker
    {
        private readonly TimeProvider _timeProvider;
        private readonly object _lock = new();
        private Dictionary<(Guid PropertyId, DateTime Date), UsageCountDto> _counts = new();

        public UsageTracker(TimeProvider timeProvider)
        {
            _timeProvider = timeProvider;
        }

        public void RecordLogin(Guid propertyId) => Add(propertyId, 1, 0, 0);

        public void RecordPush(Guid propertyId) => Add(propertyId, 0, 1, 0);

        public void RecordPull(Guid propertyId) => Add(propertyId, 0, 0, 1);

        public IReadOnlyList<UsageCountDto> Drain()
        {
            lock (_lock)
            {
                var drained = _counts.Values.ToList();
                _counts = new();
                return drained;
            }
        }

        public void Restore(IEnumerable<UsageCountDto> counts)
        {
            lock (_lock)
            {
                foreach (var count in counts)
                    Merge(count.PropertyId, count.Date, count.Logins, count.Pushes, count.Pulls);
            }
        }

        private void Add(Guid propertyId, int logins, int pushes, int pulls)
        {
            var date = BrazilTime.ToLocalDate(_timeProvider.GetUtcNow().UtcDateTime);

            lock (_lock)
                Merge(propertyId, date, logins, pushes, pulls);
        }

        private void Merge(Guid propertyId, DateTime date, int logins, int pushes, int pulls)
        {
            if (!_counts.TryGetValue((propertyId, date), out var count))
            {
                count = new UsageCountDto { PropertyId = propertyId, Date = date };
                _counts[(propertyId, date)] = count;
            }

            count.Logins += logins;
            count.Pushes += pushes;
            count.Pulls += pulls;
        }
    }
}
