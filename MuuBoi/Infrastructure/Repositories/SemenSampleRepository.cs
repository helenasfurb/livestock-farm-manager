using Microsoft.EntityFrameworkCore;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Models;
using MuuBoi.Infrastructure.Data;

namespace MuuBoi.Infrastructure.Repositories
{
    public class SemenSampleRepository : ISemenSampleRepository
    {
        private readonly ApplicationDbContext _context;

        public SemenSampleRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<SemenSample>> GetAllAsync(SemenSampleFilterDto filter)
        {
            var query = _context.SemenSamples.AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Name))
                query = query.Where(s => s.Name.Contains(filter.Name));

            if (filter.BullBreed.HasValue)
                query = query.Where(s => s.BullBreed == filter.BullBreed);

            if (filter.IsActive.HasValue)
                query = query.Where(s => s.IsActive == filter.IsActive);

            return await query.OrderBy(s => s.Name).ToListAsync();
        }

        public async Task<IEnumerable<SemenSample>> GetAutocompleteAsync(string? name)
        {
            var query = _context.SemenSamples.Where(s => s.IsActive);

            if (!string.IsNullOrWhiteSpace(name))
                query = query.Where(s => s.Name.Contains(name));

            return await query.OrderBy(s => s.Name).ToListAsync();
        }

        public async Task<SemenSample?> GetByIdAsync(int id)
        {
            return await _context.SemenSamples.FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<SemenSample> CreateAsync(SemenSample semenSample)
        {
            _context.SemenSamples.Add(semenSample);
            await _context.SaveChangesAsync();
            return semenSample;
        }

        public async Task<SemenSample> UpdateAsync(SemenSample semenSample)
        {
            _context.SemenSamples.Update(semenSample);
            await _context.SaveChangesAsync();
            return semenSample;
        }
    }
}
