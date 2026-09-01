using Microsoft.EntityFrameworkCore;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Models;
using MuuBoi.Infrastructure.Data;

namespace MuuBoi.Infrastructure.Repositories
{
    public class LactationRepository : ILactationRepository
    {
        private readonly ApplicationDbContext _context;

        public LactationRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Lactation>> GetByAnimalIdAsync(int animalId)
        {
            return await _context.Lactations
                .Where(l => l.AnimalId == animalId && l.IsActive)
                .OrderByDescending(l => l.StartDate)
                .ToListAsync();
        }

        public async Task<Lactation?> GetByIdAsync(int id)
        {
            return await _context.Lactations
                .Include(l => l.Animal)
                .FirstOrDefaultAsync(l => l.Id == id);
        }

        public async Task<Lactation?> GetOpenByAnimalIdAsync(int animalId)
        {
            return await _context.Lactations
                .Include(l => l.Animal)
                .FirstOrDefaultAsync(l => l.AnimalId == animalId && l.IsActive && l.EndDate == null);
        }

        public async Task<bool> HasOpenByAnimalIdAsync(int animalId)
        {
            return await _context.Lactations
                .AnyAsync(l => l.AnimalId == animalId && l.IsActive && l.EndDate == null);
        }

        public async Task<Lactation?> GetByCalvingIdAsync(int calvingId)
        {
            return await _context.Lactations
                .FirstOrDefaultAsync(l => l.CalvingId == calvingId);
        }

        public async Task<IEnumerable<Lactation>> GetActiveByAnimalIdsAsync(IEnumerable<int> animalIds)
        {
            var ids = animalIds.ToList();
            if (ids.Count == 0)
                return new List<Lactation>();

            return await _context.Lactations
                .Where(l => l.IsActive && ids.Contains(l.AnimalId))
                .ToListAsync();
        }

        public async Task<Lactation> CreateAsync(Lactation lactation)
        {
            _context.Lactations.Add(lactation);
            await _context.SaveChangesAsync();
            return lactation;
        }

        public async Task<Lactation> UpdateAsync(Lactation lactation)
        {
            _context.Lactations.Update(lactation);
            await _context.SaveChangesAsync();
            return lactation;
        }
    }
}
