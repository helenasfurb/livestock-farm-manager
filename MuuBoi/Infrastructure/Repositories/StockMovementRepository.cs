using Microsoft.EntityFrameworkCore;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Enums;
using MuuBoi.Domain.Models;
using MuuBoi.Infrastructure.Data;

namespace MuuBoi.Infrastructure.Repositories
{
    public class StockMovementRepository : IStockMovementRepository
    {
        private readonly ApplicationDbContext _context;

        public StockMovementRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<StockMovement>> GetByStockItemIdAsync(int stockItemId, StockMovementFilterDto filter)
        {
            var query = _context.StockMovements
                .Where(m => m.StockItemId == stockItemId && m.IsActive);

            if (filter.MovementType.HasValue)
                query = query.Where(m => m.MovementType == filter.MovementType);

            if (filter.MovementReason.HasValue)
                query = query.Where(m => m.MovementReason == filter.MovementReason);

            if (filter.DateFrom.HasValue)
                query = query.Where(m => m.MovementDate >= filter.DateFrom.Value.Date);

            if (filter.DateTo.HasValue)
                query = query.Where(m => m.MovementDate < filter.DateTo.Value.Date.AddDays(1));

            return await query.OrderByDescending(m => m.MovementDate).ThenByDescending(m => m.Id).ToListAsync();
        }

        public async Task<StockMovement?> GetByIdAsync(int id)
        {
            return await _context.StockMovements
                .Include(m => m.StockItem)
                .FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<StockMovement> CreateAsync(StockMovement movement)
        {
            _context.StockMovements.Add(movement);
            await _context.SaveChangesAsync();
            return movement;
        }

        public async Task<StockMovement> UpdateAsync(StockMovement movement)
        {
            _context.StockMovements.Update(movement);
            await _context.SaveChangesAsync();
            return movement;
        }

        public async Task<StockItemLevels> GetLevelsAsync(int stockItemId)
        {
            var groups = await _context.StockMovements
                .Where(m => m.StockItemId == stockItemId && m.IsActive)
                .GroupBy(m => m.MovementType)
                .Select(g => new
                {
                    Type = g.Key,
                    Qty = g.Sum(m => m.Quantity),
                    InValue = g.Sum(m => m.TotalValue ?? 0m),
                    OutValue = g.Sum(m => (m.UnitCostSnapshot ?? 0m) * m.Quantity)
                })
                .ToListAsync();

            return BuildLevels(
                groups.FirstOrDefault(g => g.Type == StockMovementType.Input) is { } i ? (i.Qty, i.InValue) : (0m, 0m),
                groups.FirstOrDefault(g => g.Type == StockMovementType.Output) is { } o ? (o.Qty, o.OutValue) : (0m, 0m));
        }

        public async Task<Dictionary<int, StockItemLevels>> GetLevelsBatchAsync(IEnumerable<int> stockItemIds)
        {
            var ids = stockItemIds.Distinct().ToList();
            if (ids.Count == 0)
                return new Dictionary<int, StockItemLevels>();

            var groups = await _context.StockMovements
                .Where(m => ids.Contains(m.StockItemId) && m.IsActive)
                .GroupBy(m => new { m.StockItemId, m.MovementType })
                .Select(g => new
                {
                    g.Key.StockItemId,
                    g.Key.MovementType,
                    Qty = g.Sum(m => m.Quantity),
                    InValue = g.Sum(m => m.TotalValue ?? 0m),
                    OutValue = g.Sum(m => (m.UnitCostSnapshot ?? 0m) * m.Quantity)
                })
                .ToListAsync();

            return ids.ToDictionary(id => id, id =>
            {
                var input = groups.FirstOrDefault(g => g.StockItemId == id && g.MovementType == StockMovementType.Input);
                var output = groups.FirstOrDefault(g => g.StockItemId == id && g.MovementType == StockMovementType.Output);
                return BuildLevels(
                    input is null ? (0m, 0m) : (input.Qty, input.InValue),
                    output is null ? (0m, 0m) : (output.Qty, output.OutValue));
            });
        }

        public async Task<Dictionary<int, decimal>> GetConsumptionSinceBatchAsync(IEnumerable<int> stockItemIds, DateTime since)
        {
            var ids = stockItemIds.Distinct().ToList();
            if (ids.Count == 0)
                return new Dictionary<int, decimal>();

            var groups = await _context.StockMovements
                .Where(m => ids.Contains(m.StockItemId) && m.IsActive
                    && m.MovementType == StockMovementType.Output
                    && m.MovementReason == StockMovementReason.Consumption
                    && m.MovementDate >= since.Date)
                .GroupBy(m => m.StockItemId)
                .Select(g => new { StockItemId = g.Key, Qty = g.Sum(m => m.Quantity) })
                .ToListAsync();

            return groups.ToDictionary(g => g.StockItemId, g => g.Qty);
        }

        public async Task<decimal> GetPeriodPurchaseTotalAsync(DateTime dateFrom, DateTime dateTo, IReadOnlyCollection<int>? stockItemIds = null)
        {
            var toExclusive = dateTo.Date.AddDays(1);
            var query = _context.StockMovements
                .Where(m => m.IsActive && m.MovementReason == StockMovementReason.Purchase
                    && m.MovementDate >= dateFrom.Date && m.MovementDate < toExclusive);

            if (stockItemIds != null)
                query = query.Where(m => stockItemIds.Contains(m.StockItemId));

            return await query.SumAsync(m => m.TotalValue ?? 0m);
        }

        public async Task<decimal> GetPeriodConsumedValueAsync(DateTime dateFrom, DateTime dateTo, IReadOnlyCollection<int>? stockItemIds = null)
        {
            var toExclusive = dateTo.Date.AddDays(1);
            var query = _context.StockMovements
                .Where(m => m.IsActive && m.MovementType == StockMovementType.Output
                    && m.MovementReason == StockMovementReason.Consumption
                    && m.MovementDate >= dateFrom.Date && m.MovementDate < toExclusive);

            if (stockItemIds != null)
                query = query.Where(m => stockItemIds.Contains(m.StockItemId));

            return await query.SumAsync(m => (m.UnitCostSnapshot ?? 0m) * m.Quantity);
        }

        public async Task<Dictionary<int, decimal>> GetConsumedQuantityByItemAsync(DateTime dateFrom, DateTime dateTo, IReadOnlyCollection<int>? stockItemIds = null)
        {
            var toExclusive = dateTo.Date.AddDays(1);
            var query = _context.StockMovements
                .Where(m => m.IsActive && m.MovementType == StockMovementType.Output
                    && m.MovementReason == StockMovementReason.Consumption
                    && m.MovementDate >= dateFrom.Date && m.MovementDate < toExclusive);

            if (stockItemIds != null)
                query = query.Where(m => stockItemIds.Contains(m.StockItemId));

            var groups = await query
                .GroupBy(m => m.StockItemId)
                .Select(g => new { StockItemId = g.Key, Qty = g.Sum(m => m.Quantity) })
                .ToListAsync();

            return groups.ToDictionary(g => g.StockItemId, g => g.Qty);
        }

        private static StockItemLevels BuildLevels((decimal Qty, decimal Value) input, (decimal Qty, decimal Value) output)
            => new(input.Qty - output.Qty, input.Value - output.Value);
    }
}
