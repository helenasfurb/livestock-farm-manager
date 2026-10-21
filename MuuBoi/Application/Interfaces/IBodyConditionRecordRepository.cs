using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Interfaces
{
    public interface IBodyConditionRecordRepository
    {
        Task<IEnumerable<BodyConditionRecord>> GetByAnimalIdAsync(int animalId);
        Task<BodyConditionRecord?> GetByIdAsync(int id, int animalId);
        Task<BodyConditionRecord?> GetBySyncIdAsync(Guid syncId);
        Task<BodyConditionRecord> CreateAsync(BodyConditionRecord record);
        Task<BodyConditionRecord> UpdateAsync(BodyConditionRecord record);
        Task<IReadOnlyList<BodyConditionRecord>> GetChangesAsync(ulong since, int take);
    }
}
