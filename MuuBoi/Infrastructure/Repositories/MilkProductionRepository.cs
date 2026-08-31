using Microsoft.EntityFrameworkCore;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Models;
using MuuBoi.Infrastructure.Data;

namespace MuuBoi.Infrastructure.Repositories
{
    public class MilkProductionRepository : IMilkProductionRepository
    {
        private readonly ApplicationDbContext _context;

        public MilkProductionRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<MilkProduction>> GetAllAsync(MilkProductionFilterDto filter)
        {
            var query = _context.MilkProductions.AsQueryable();

            if (filter.DateFrom.HasValue)
                query = query.Where(m => m.Date >= filter.DateFrom);

            if (filter.DateTo.HasValue)
                query = query.Where(m => m.Date <= filter.DateTo);

            if (filter.Milking.HasValue)
                query = query.Where(m => m.Milking == filter.Milking);

            if (filter.IsActive.HasValue)
                query = query.Where(m => m.IsActive == filter.IsActive);

            return await query.OrderByDescending(m => m.Date).ToListAsync();
        }

        public async Task<MilkProduction?> GetByIdAsync(int id)
        {
            return await _context.MilkProductions.FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<MilkProduction> CreateAsync(MilkProduction milkProduction)
        {
            _context.MilkProductions.Add(milkProduction);
            await _context.SaveChangesAsync();
            return milkProduction;
        }

        public async Task<MilkProduction> UpdateAsync(MilkProduction milkProduction)
        {
            _context.MilkProductions.Update(milkProduction);
            await _context.SaveChangesAsync();
            return milkProduction;
        }
    }
}
