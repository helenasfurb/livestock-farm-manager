using MuuBoi.Application.DTOs;

namespace MuuBoi.Application.Interfaces
{
    public interface IStockMovementService
    {
        Task<IEnumerable<StockMovementListItemDto>> GetByStockItemIdAsync(int stockItemId, StockMovementFilterDto filter);
        Task<StockMovementDto> GetByIdAsync(int stockItemId, int movementId);
        Task<StockMovementDto> CreateAsync(int stockItemId, StockMovementCreateDto dto);
        Task<StockMovementDto> UpdateAsync(int stockItemId, int movementId, StockMovementUpdateDto dto);
        Task DeactivateAsync(int stockItemId, int movementId);
        Task<SyncPageDto<StockMovementDto>> GetChangesAsync(string? since, int? limit);
    }
}
