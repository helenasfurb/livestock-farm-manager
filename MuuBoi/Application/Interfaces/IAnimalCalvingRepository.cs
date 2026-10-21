using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Interfaces
{
    public interface IAnimalCalvingRepository
    {
        Task<AnimalCalving?> GetByIdAsync(int id);
        Task<AnimalCalving?> GetBySyncIdAsync(Guid syncId);
        Task<AnimalCalving?> CreateAsync(AnimalCalving calving, Lactation lactation, BodyConditionRecord? bodyCondition);
        Task<AnimalCalving> UpdateAsync(AnimalCalving calving);
        Task<bool> HasActiveByPregnancyIdAsync(int pregnancyId);
        Task<AnimalCalving?> GetLastActiveByAnimalIdAsync(int animalId);
        Task<List<(int AnimalId, DateTime CalvingDate)>> GetActiveCalvingDatesAsync();
        Task<IReadOnlyList<AnimalCalving>> GetRecentActiveByAnimalIdAsync(int animalId, int count);
        Task<AnimalCalvingCalf?> GetParentageByAnimalIdAsync(int animalId);
        Task<AnimalCalvingCalf?> GetCalfByIdAsync(int calfId);
        Task<AnimalCalvingCalf?> GetActiveCalfByAnimalIdAsync(int animalId);
        Task<AnimalCalvingCalf> UpdateCalfAsync(AnimalCalvingCalf calf);
        Task<IReadOnlyList<AnimalCalving>> GetChangesAsync(ulong since, int take);
        Task<List<AnimalCalvingCalf>> GetCalvesByCalvingIdsAsync(IReadOnlyCollection<int> calvingIds);
    }
}
