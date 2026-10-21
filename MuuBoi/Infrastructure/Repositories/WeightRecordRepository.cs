using Microsoft.EntityFrameworkCore;
using MuuBoi.Application.Interfaces;
using MuuBoi.Infrastructure.Data;
using MuuBoi.Domain.Models;

namespace MuuBoi.Infrastructure.Repositories
{
    public class WeightRecordRepository : IWeightRecordRepository
    {
        private readonly ApplicationDbContext _context;

        public WeightRecordRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<WeightRecord>> GetAllWeightRecordsAsync(int animalId)
        {
            return await _context.WeightRecords
                .Where(w => w.AnimalId == animalId && w.IsActive)
                .OrderBy(w => w.RecordedAt)
                .ToListAsync();
        }

        public async Task<WeightRecord?> GetWeightRecordByIdAsync(int id, int animalId)
        {
            return await _context.WeightRecords
                .FirstOrDefaultAsync(w => w.Id == id && w.AnimalId == animalId);
        }

        public async Task<WeightRecord?> GetWeightRecordBySyncIdAsync(Guid syncId)
        {
            return await _context.FindBySyncIdAsync<WeightRecord>(syncId);
        }

        public async Task<WeightRecord> CreateWeightRecordAsync(WeightRecord weightRecord)
        {
            return await _context.AddSyncableAsync(weightRecord);
        }

        public async Task<WeightRecord?> UpdateWeightRecordAsync(WeightRecord weightRecord)
        {
            _context.WeightRecords.Update(weightRecord);
            await _context.SaveChangesAsync();
            return weightRecord;
        }

        public async Task<WeightRecord?> DeleteWeightRecordAsync(int id, int animalId)
        {
            var weightRecord = await GetWeightRecordByIdAsync(id, animalId);
            if (weightRecord == null) return null;

            weightRecord.IsActive = false;
            weightRecord.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return weightRecord;
        }

        public async Task<IReadOnlyList<WeightRecord>> GetChangesAsync(ulong since, int take)
        {
            return await _context.GetChangesSinceAsync<WeightRecord>(since, take);
        }
    }
}
