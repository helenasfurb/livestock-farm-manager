using Microsoft.EntityFrameworkCore;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Models;
using MuuBoi.Infrastructure.Data;

namespace MuuBoi.Infrastructure.Repositories
{
    public class AnimalExitRecordRepository : IAnimalExitRecordRepository
    {
        private readonly ApplicationDbContext _context;

        public AnimalExitRecordRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<AnimalExitRecord>> GetByAnimalIdAsync(int animalId)
        {
            return await _context.AnimalExitRecords
                .Where(r => r.AnimalId == animalId)
                .OrderByDescending(r => r.ExitDate)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<AnimalExitRecord>> GetByAnimalIdsAsync(IReadOnlyCollection<int> animalIds)
        {
            if (animalIds.Count == 0)
                return new List<AnimalExitRecord>();

            return await _context.AnimalExitRecords
                .Where(r => animalIds.Contains(r.AnimalId))
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
