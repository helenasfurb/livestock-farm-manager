using MuuBoi.Application.DTOs;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Interfaces
{
    public interface IStockItemRepository
    {
        Task<IEnumerable<StockItem>> GetAllAsync(StockItemFilterDto filter);
        Task<StockItem?> GetByIdAsync(int id);
        Task<StockItem?> GetBySyncIdAsync(Guid syncId);
        Task<Dictionary<int, StockItem>> GetByIdsAsync(IEnumerable<int> ids);
        Task<StockItem> CreateAsync(StockItem item);
        Task<StockItem> UpdateAsync(StockItem item);
        Task<IReadOnlyList<StockItem>> GetChangesAsync(ulong since, int take);
    }
}
