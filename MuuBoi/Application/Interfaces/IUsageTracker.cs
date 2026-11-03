using MuuBoi.Application.DTOs;

namespace MuuBoi.Application.Interfaces
{
    public interface IUsageTracker
    {
        void RecordLogin(Guid propertyId);
        void RecordPush(Guid propertyId);
        void RecordPull(Guid propertyId);
        IReadOnlyList<UsageCountDto> Drain();
        void Restore(IEnumerable<UsageCountDto> counts);
    }
}
