using MuuBoi.Application.DTOs;

namespace MuuBoi.Application.Interfaces
{
    public interface IWeightRecordService
    {
        Task<IEnumerable<WeightRecordDto>> GetAllWeightRecordsAsync(int animalId);
        Task<WeightRecordDto> GetWeightRecordByIdAsync(int id, int animalId);
        Task<WeightRecordDto> CreateWeightRecordAsync(WeightRecordCreateDto weightRecordCreateDto, int animalId);
        Task<bool> DeleteWeightRecordAsync(int id, int animalId);
        Task<WeightRecordDto> UpdateWeightRecordAsync(int id, int animalId, WeightRecordUpdateDto weightRecordUpdateDto);
        Task<SyncPageDto<WeightRecordDto>> GetChangesAsync(string? since, int? limit);
    }
}
