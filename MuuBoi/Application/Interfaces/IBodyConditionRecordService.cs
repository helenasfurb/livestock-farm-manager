using MuuBoi.Application.DTOs;

namespace MuuBoi.Application.Interfaces
{
    public interface IBodyConditionRecordService
    {
        Task<IEnumerable<BodyConditionRecordDto>> GetByAnimalIdAsync(int animalId);
        Task<BodyConditionRecordDto> CreateAsync(int animalId, BodyConditionRecordCreateDto dto);
        Task<BodyConditionRecordDto> UpdateAsync(int animalId, int recordId, BodyConditionRecordUpdateDto dto);
    }
}
