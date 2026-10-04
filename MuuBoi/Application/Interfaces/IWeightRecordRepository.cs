using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Interfaces
{
    public interface IWeightRecordRepository
    {
        Task<IEnumerable<WeightRecord>> GetAllWeightRecordsAsync(int animalId);
        Task<WeightRecord?> GetWeightRecordByIdAsync(int id, int animalId);
        Task<WeightRecord> CreateWeightRecordAsync(WeightRecord weightRecord);
        Task<WeightRecord?> UpdateWeightRecordAsync(WeightRecord weightRecord);
        Task<WeightRecord?> DeleteWeightRecordAsync(int id, int animalId);
    }
}
