using MuuBoi.Application.DTOs;

namespace MuuBoi.Application.Interfaces
{
    public interface IStockItemService
    {
        Task<IEnumerable<StockItemListItemDto>> GetAllAsync(StockItemFilterDto filter);
        Task<StockItemDto> GetByIdAsync(int id);
        Task<StockItemCreatedDto> CreateAsync(StockItemCreateDto dto);
        Task<StockItemDto> UpdateAsync(int id, StockItemUpdateDto dto);
        Task DeactivateAsync(int id);
        Task<StockDashboardDto> GetDashboardAsync(StockDashboardFilterDto filter);
        Task<IEnumerable<StockAlertDto>> GetAlertsAsync();
        Task<SyncPageDto<StockItemDto>> GetChangesAsync(string? since, int? limit);
    }
}
