using Microsoft.EntityFrameworkCore;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Enums;
using MuuBoi.Domain.Models;
using MuuBoi.Infrastructure.Data;

namespace MuuBoi.Infrastructure.Repositories
{
    public class AnimalRepository : IAnimalRepository
    {
        private readonly ApplicationDbContext _context;

        public AnimalRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Animal>> GetAllAnimalsAsync(AnimalFilterDto filter)
        {
            var query = ApplyFilters(_context.Animals
                .Include(a => a.WeightRecords!.OrderByDescending(w => w.RecordedAt).Take(1))
                .Include(a => a.ExitRecords!.OrderByDescending(e => e.ExitDate).Take(1)), filter);

            return await query.ToListAsync();
        }

        public async Task<Dictionary<int, ReproductiveStatus>> GetReproductiveStatusMapAsync(IReadOnlyCollection<int> animalIds)
        {
            var rows = await _context.Animals
                .Where(a => animalIds.Contains(a.Id))
                .Select(a => new
                {
                    a.Id,
                    HasConfirmedPregnancy = a.Pregnancies!.Any(p =>
                        p.IsActive && p.Status == AnimalPregnancyStatus.Confirmed),
                    LastCalvingDate = a.Calvings!
                        .Where(c => c.IsActive)
                        .Max(c => (DateTime?)c.CalvingDate),
                    LastAwaitingBreedingDate = a.BreedingEvents!
                        .Where(e => e.IsActive && e.Status == ReproductiveEventStatus.AwaitingDiagnosis)
                        .Max(e => (DateTime?)e.BreedingDate)
                })
                .ToListAsync();

            var now = DateTime.UtcNow;
            return rows.ToDictionary(
                r => r.Id,
                r => ReproductiveStatusResolver.Resolve(
                    r.HasConfirmedPregnancy, r.LastCalvingDate, r.LastAwaitingBreedingDate, now));
        }

        private static IQueryable<Animal> ApplyFilters(IQueryable<Animal> query, AnimalFilterDto filter)
        {
            if (filter.IsActive.HasValue)
                query = query.Where(a => a.IsActive == filter.IsActive.Value);

            if (!string.IsNullOrWhiteSpace(filter.TagNumber))
                query = query.Where(a => a.TagNumber != null && a.TagNumber.Contains(filter.TagNumber));

            if (!string.IsNullOrWhiteSpace(filter.Name))
                query = query.Where(a => a.Name != null && a.Name.Contains(filter.Name));

            if (filter.Classification.HasValue)
                query = query.Where(a => a.Classification == filter.Classification.Value);

            if (filter.Breed.HasValue)
                query = query.Where(a => a.Breed == filter.Breed.Value);

            return query;
        }

        public async Task<Animal?> GetAnimalByIdAsync(int id)
        {
            return await _context.Animals
                .Include(a => a.WeightRecords!.OrderByDescending(w => w.RecordedAt))
                .Include(a => a.BodyConditionRecords!.OrderByDescending(r => r.RecordedAt).Take(1))
                .Include(a => a.ExitRecords!.OrderByDescending(e => e.ExitDate).Take(1))
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<Animal> CreateAnimalAsync(Animal animal)
        {
            _context.Animals.Add(animal);
            await _context.SaveChangesAsync();
            return animal;
        }

        public async Task<Animal> UpdateAnimalAsync(Animal animal)
        {
            _context.Animals.Update(animal);
            await _context.SaveChangesAsync();
            return animal;
        }

        public async Task<bool> TagNumberExistsAsync(string tagNumber, int? excludeAnimalId = null)
        {
            var query = _context.Animals.Where(a => a.TagNumber == tagNumber);

            if (excludeAnimalId.HasValue)
                query = query.Where(a => a.Id != excludeAnimalId.Value);

            return await query.AnyAsync();
        }
    }
}
