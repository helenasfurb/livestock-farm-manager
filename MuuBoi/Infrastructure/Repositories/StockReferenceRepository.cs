using Microsoft.EntityFrameworkCore;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Models;
using MuuBoi.Infrastructure.Data;

namespace MuuBoi.Infrastructure.Repositories
{
    public class StockReferenceRepository : IStockReferenceRepository
    {
        private readonly ApplicationDbContext _context;

        public StockReferenceRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<StockCategory>> GetCategoriesAsync()
        {
            return await _context.StockCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Id)
                .ToListAsync();
        }

        public async Task<IEnumerable<UnitOfMeasure>> GetUnitsAsync()
        {
            return await _context.UnitsOfMeasure
                .Where(u => u.IsActive)
                .OrderBy(u => u.Id)
                .ToListAsync();
        }

        public async Task<bool> CategoryExistsAsync(int id)
        {
            return await _context.StockCategories.AnyAsync(c => c.Id == id && c.IsActive);
        }

        public async Task<bool> UnitExistsAsync(int id)
        {
            return await _context.UnitsOfMeasure.AnyAsync(u => u.Id == id && u.IsActive);
        }
    }
}
