using Microsoft.EntityFrameworkCore;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Models;
using MuuBoi.Infrastructure.Data;

namespace MuuBoi.Infrastructure.Repositories
{
    public class StockItemRepository : IStockItemRepository
    {
        private readonly ApplicationDbContext _context;

        public StockItemRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<StockItem>> GetAllAsync(StockItemFilterDto filter)
        {
            var query = _context.StockItems
                .Include(i => i.StockCategory)
                .Include(i => i.UnitOfMeasure)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Name))
                query = query.Where(i => i.Name.Contains(filter.Name));

            if (filter.StockCategoryId.HasValue)
                query = query.Where(i => i.StockCategoryId == filter.StockCategoryId);

            if (filter.IsActive.HasValue)
                query = query.Where(i => i.IsActive == filter.IsActive);

            return await query.OrderBy(i => i.Name).ToListAsync();
        }

        public async Task<StockItem?> GetByIdAsync(int id)
        {
            return await _context.StockItems
                .Include(i => i.StockCategory)
                .Include(i => i.UnitOfMeasure)
                .FirstOrDefaultAsync(i => i.Id == id);
        }

        public async Task<StockItem> CreateAsync(StockItem item)
        {
            _context.StockItems.Add(item);
            await _context.SaveChangesAsync();
            return item;
        }

        public async Task<StockItem> UpdateAsync(StockItem item)
        {
            _context.StockItems.Update(item);
            await _context.SaveChangesAsync();
            return item;
        }
    }
}
