using MuuBoi.Application.DTOs;

namespace MuuBoi.Application.Interfaces
{
    public interface IPortalRepository
    {
        Task<PortalTotalsFact> GetTotalsAsync(DateTime nowUtc, DateTime newSinceUtc);
        Task<IReadOnlyList<PortalDailyUsageDto>> GetDailyUsageAsync(DateTime fromDate);
        Task<IReadOnlyList<PortalMonthFact>> GetNewPropertiesByMonthAsync(DateTime fromUtc);
        Task<PortalPropertyPageDto> GetPropertiesAsync(PortalPropertyFilterDto filter, DateTime usageFromDate);
    }
}
