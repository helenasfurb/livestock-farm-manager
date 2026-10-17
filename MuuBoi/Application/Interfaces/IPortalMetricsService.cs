using MuuBoi.Application.DTOs;

namespace MuuBoi.Application.Interfaces
{
    public interface IPortalMetricsService
    {
        Task<PortalSummaryDto> GetSummaryAsync(PortalSummaryFilterDto filter);
        Task<PortalPropertyPageDto> GetPropertiesAsync(PortalPropertyFilterDto filter);
    }
}
