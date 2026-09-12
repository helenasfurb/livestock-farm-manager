using MuuBoi.Application.DTOs;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Interfaces
{
    public readonly record struct StockItemLevels(decimal Quantity, decimal Value);

    public interface IStockMovementRepository
    {
        Task<IEnumerable<StockMovement>> GetByStockItemIdAsync(int stockItemId, StockMovementFilterDto filter);
        Task<StockMovement?> GetByIdAsync(int id);
        Task<StockMovement> CreateAsync(StockMovement movement);
        Task<StockMovement> UpdateAsync(StockMovement movement);
        Task<StockItemLevels> GetLevelsAsync(int stockItemId);
        Task<Dictionary<int, StockItemLevels>> GetLevelsBatchAsync(IEnumerable<int> stockItemIds);
        Task<Dictionary<int, decimal>> GetConsumptionSinceBatchAsync(IEnumerable<int> stockItemIds, DateTime since);
        Task<decimal> GetPeriodPurchaseTotalAsync(DateTime dateFrom, DateTime dateTo, IReadOnlyCollection<int>? stockItemIds = null);
        Task<decimal> GetPeriodConsumedValueAsync(DateTime dateFrom, DateTime dateTo, IReadOnlyCollection<int>? stockItemIds = null);
        Task<Dictionary<int, decimal>> GetConsumedQuantityByItemAsync(DateTime dateFrom, DateTime dateTo, IReadOnlyCollection<int>? stockItemIds = null);
    }
}
