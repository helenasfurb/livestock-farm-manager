using Microsoft.EntityFrameworkCore;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Models;
using MuuBoi.Infrastructure.Data;

namespace MuuBoi.Infrastructure.Repositories
{
    public class BodyConditionRecordRepository : IBodyConditionRecordRepository
    {
        private readonly ApplicationDbContext _context;

        public BodyConditionRecordRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<BodyConditionRecord>> GetByAnimalIdAsync(int animalId)
        {
            return await _context.BodyConditionRecords
                .Where(r => r.AnimalId == animalId)
                .OrderByDescending(r => r.RecordedAt)
                .ToListAsync();
        }

        public async Task<BodyConditionRecord?> GetByIdAsync(int id, int animalId)
        {
            return await _context.BodyConditionRecords
                .FirstOrDefaultAsync(r => r.Id == id && r.AnimalId == animalId);
        }

        public async Task<BodyConditionRecord> CreateAsync(BodyConditionRecord record)
        {
            _context.BodyConditionRecords.Add(record);
            await _context.SaveChangesAsync();
            return record;
        }

        public async Task<BodyConditionRecord> UpdateAsync(BodyConditionRecord record)
        {
            _context.BodyConditionRecords.Update(record);
            await _context.SaveChangesAsync();
            return record;
        }
    }
}
