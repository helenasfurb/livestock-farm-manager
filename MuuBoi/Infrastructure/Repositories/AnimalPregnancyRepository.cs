using Microsoft.EntityFrameworkCore;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Enums;
using MuuBoi.Domain.Models;
using MuuBoi.Infrastructure.Data;

namespace MuuBoi.Infrastructure.Repositories
{
    public class AnimalPregnancyRepository : IAnimalPregnancyRepository
    {
        private readonly ApplicationDbContext _context;

        public AnimalPregnancyRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<AnimalPregnancy>> GetAllAsync(AnimalPregnancyFilterDto filter)
        {
            var query = _context.AnimalPregnancies
                .Include(p => p.Animal)
                .AsQueryable();

            if (filter.AnimalId.HasValue)
                query = query.Where(p => p.AnimalId == filter.AnimalId.Value);

            if (filter.Status.HasValue)
                query = query.Where(p => p.Status == filter.Status.Value);

            if (filter.IsActive.HasValue)
                query = query.Where(p => p.IsActive == filter.IsActive.Value);

            return await query
                .OrderByDescending(p => p.ConfirmationDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<AnimalPregnancy>> GetByAnimalIdAsync(int animalId, bool? isActive)
        {
            var query = _context.AnimalPregnancies
                .Include(p => p.Animal)
                .Where(p => p.AnimalId == animalId)
                .AsQueryable();

            if (isActive.HasValue)
                query = query.Where(p => p.IsActive == isActive.Value);

            return await query
                .OrderByDescending(p => p.ConfirmationDate)
                .ToListAsync();
        }

        public async Task<AnimalPregnancy?> GetByIdAsync(int id)
        {
            return await _context.AnimalPregnancies
                .Include(p => p.Animal)
                .Include(p => p.Calvings!.Where(c => c.IsActive))
                    .ThenInclude(c => c.Calves!.Where(cf => cf.IsActive))
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<AnimalPregnancy> CreateAsync(AnimalPregnancy pregnancy)
        {
            _context.AnimalPregnancies.Add(pregnancy);
            await _context.SaveChangesAsync();
            return pregnancy;
        }

        public async Task<AnimalPregnancy> UpdateAsync(AnimalPregnancy pregnancy)
        {
            _context.AnimalPregnancies.Update(pregnancy);
            await _context.SaveChangesAsync();
            return pregnancy;
        }

        public async Task<bool> ExistsActiveForBreedingEventAsync(int breedingEventId)
        {
            return await _context.AnimalPregnancies
                .AnyAsync(p => p.BreedingEventId == breedingEventId && p.IsActive);
        }

        public async Task<bool> HasActiveConfirmedByAnimalIdAsync(int animalId)
        {
            return await _context.AnimalPregnancies
                .AnyAsync(p => p.AnimalId == animalId
                    && p.IsActive
                    && p.Status == AnimalPregnancyStatus.Confirmed);
        }
    }
}
