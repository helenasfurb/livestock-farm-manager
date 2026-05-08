using MuuBoi.Application.DTOs;
using MuuBoi.DTOs;

namespace MuuBoi.Application.Interfaces
{
    public interface IWeightRecordService
    {
        Task<IEnumerable<WeightRecordDto>> GetAllWeightRecordsAsync(string animalId);
        Task<WeightRecordDto?> GetWeightRecordByIdAsync(int id, string animalId);
        Task<WeightRecordDto> CreateWeightRecordAsync(WeightRecordCreateDto weightRecordCreateDto, string animalId);
        Task<WeightRecordDto?> DeleteWeightRecordAsync(int id, string animalId);
        Task<WeightRecordDto?> UpdateWeightRecordAsync(int id, string animalId, WeightRecordUpdateDto weightRecordUpdateDto);
    }
}
