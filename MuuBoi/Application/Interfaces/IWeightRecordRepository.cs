using MuuBoi.Models;

namespace MuuBoi.Application.Interfaces
{
    public interface IWeightRecordRepository
    {
        Task<IEnumerable<WeightRecord>> GetAllWeightRecordsAsync(string animalId);
        Task<WeightRecord?> GetWeightRecordByIdAsync(int id, string animalId);
        Task<WeightRecord> CreateWeightRecordAsync(WeightRecord weightRecord);
        Task<WeightRecord?> UpdateWeightRecordAsync(WeightRecord weightRecord);
        Task<WeightRecord?> DeleteWeightRecordAsync(int id, string animalId);
    }
}
