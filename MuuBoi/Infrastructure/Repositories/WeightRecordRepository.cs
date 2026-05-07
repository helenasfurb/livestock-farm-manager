using Microsoft.EntityFrameworkCore;
using MuuBoi.Application.Interfaces;
using MuuBoi.Data;
using MuuBoi.Models;

namespace MuuBoi.Infrastructure.Repositories
{
    public class WeightRecordRepository : IWeightRecordRepository
    {
        private readonly ApplicationDbContext _context;

        public WeightRecordRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<WeightRecord>> GetAllWeightRecordsAsync(string animalId)
        {
            return await _context.WeightRecords
                .Where(w => w.AnimalId == int.Parse(animalId))
                .OrderBy(w => w.RecordedAt)
                .ToListAsync();
        }

        public async Task<WeightRecord?> GetWeightRecordByIdAsync(int id, string animalId)
        {
            return await _context.WeightRecords
                .FirstOrDefaultAsync(w => w.Id == id && w.AnimalId == int.Parse(animalId));
        }

        public async Task<WeightRecord> CreateWeightRecordAsync(WeightRecord weightRecord)
        {
            _context.WeightRecords.Add(weightRecord);
            await _context.SaveChangesAsync();
            return weightRecord;
        }

        public async Task<WeightRecord?> UpdateWeightRecordAsync(WeightRecord weightRecord)
        {
            _context.WeightRecords.Update(weightRecord);
            await _context.SaveChangesAsync();
            return weightRecord;
        }

        public async Task<WeightRecord?> DeleteWeightRecordAsync(int id, string animalId)
        {
            var weightRecord = await GetWeightRecordByIdAsync(id, animalId);
            if (weightRecord == null) return null;

            _context.WeightRecords.Remove(weightRecord);
            await _context.SaveChangesAsync();
            return weightRecord;
        }
    }
}
