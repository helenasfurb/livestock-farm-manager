using MuuBoi.DTOs;

namespace MuuBoi.Application.Interfaces
{
    public interface IDashboardService
    {
        Task<DashboardDto> GetDashboardAsync(string userId);
    }
}
