using Microsoft.EntityFrameworkCore;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Models;
using MuuBoi.Infrastructure.Data;

namespace MuuBoi.Infrastructure.Repositories
{
    public class AnimalCalvingRepository : IAnimalCalvingRepository
    {
        private readonly ApplicationDbContext _context;

        public AnimalCalvingRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<AnimalCalving?> GetByIdAsync(int id)
        {
            return await _context.AnimalCalvings
                .Include(c => c.AnimalPregnancy)
                .Include(c => c.Calves!)
                    .ThenInclude(cf => cf.Animal)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<List<(int AnimalId, DateTime CalvingDate)>> GetActiveCalvingDatesAsync()
        {
            var rows = await _context.AnimalCalvings
                .Where(c => c.IsActive)
                .Select(c => new { c.AnimalId, c.CalvingDate })
                .ToListAsync();

            return rows.Select(r => (r.AnimalId, r.CalvingDate)).ToList();
        }

        public async Task<AnimalCalving> CreateAsync(AnimalCalving calving)
        {
            _context.AnimalCalvings.Add(calving);
            await _context.SaveChangesAsync();
            return calving;
        }

        public async Task<AnimalCalving> UpdateAsync(AnimalCalving calving)
        {
            _context.AnimalCalvings.Update(calving);
            await _context.SaveChangesAsync();
            return calving;
        }

        public async Task<bool> HasActiveByPregnancyIdAsync(int pregnancyId)
        {
            return await _context.AnimalCalvings
                .AnyAsync(c => c.AnimalPregnancyId == pregnancyId && c.IsActive);
        }

        public async Task<AnimalCalving?> GetLastActiveByAnimalIdAsync(int animalId)
        {
            return await _context.AnimalCalvings
                .Where(c => c.AnimalId == animalId && c.IsActive)
                .OrderByDescending(c => c.CalvingDate)
                .FirstOrDefaultAsync();
        }

        public async Task<IReadOnlyList<AnimalCalving>> GetRecentActiveByAnimalIdAsync(int animalId, int count)
        {
            return await _context.AnimalCalvings
                .Where(c => c.AnimalId == animalId && c.IsActive)
                .OrderByDescending(c => c.CalvingDate)
                .Take(count)
                .ToListAsync();
        }

        public async Task<AnimalCalvingCalf?> GetParentageByAnimalIdAsync(int animalId)
        {
            return await _context.AnimalCalvingCalves
                .Include(cf => cf.Calving!)
                    .ThenInclude(c => c.Animal)
                .Include(cf => cf.Calving!)
                    .ThenInclude(c => c.AnimalPregnancy!)
                        .ThenInclude(p => p.SireAnimal)
                .Include(cf => cf.Calving!)
                    .ThenInclude(c => c.AnimalPregnancy!)
                        .ThenInclude(p => p.SemenSample)
                .Include(cf => cf.Calving!)
                    .ThenInclude(c => c.AnimalPregnancy!)
                        .ThenInclude(p => p.BreedingEvent!)
                            .ThenInclude(be => be.SireAnimal)
                .Include(cf => cf.Calving!)
                    .ThenInclude(c => c.AnimalPregnancy!)
                        .ThenInclude(p => p.BreedingEvent!)
                            .ThenInclude(be => be.SemenSample)
                .OrderByDescending(cf => cf.CreatedAt)
                .FirstOrDefaultAsync(cf => cf.AnimalId == animalId);
        }

        public async Task<AnimalCalvingCalf?> GetCalfByIdAsync(int calfId)
        {
            return await _context.AnimalCalvingCalves
                .Include(cf => cf.Calving)
                .Include(cf => cf.Animal!)
                    .ThenInclude(a => a.WeightRecords)
                .FirstOrDefaultAsync(cf => cf.Id == calfId);
        }

        public async Task<AnimalCalvingCalf?> GetActiveCalfByAnimalIdAsync(int animalId)
        {
            return await _context.AnimalCalvingCalves
                .FirstOrDefaultAsync(cf => cf.AnimalId == animalId && cf.IsActive);
        }

        public async Task<AnimalCalvingCalf> UpdateCalfAsync(AnimalCalvingCalf calf)
        {
            _context.AnimalCalvingCalves.Update(calf);
            await _context.SaveChangesAsync();
            return calf;
        }
    }
}
