using MuuBoi.Application.DTOs;

namespace MuuBoi.Application.Interfaces
{
    public interface IDashboardService
    {
        Task<DashboardDto> GetDashboardAsync();
        Task<ProductiveDashboardDto> GetProductiveDashboardAsync(ProductiveDashboardFilterDto filter);
        Task<ReproductiveDashboardDto> GetReproductiveDashboardAsync(ReproductiveDashboardFilterDto filter);
    }
}
