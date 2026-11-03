using MuuBoi.Application.DTOs;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Interfaces
{
    public interface IBreedingEventRepository
    {
        Task<IEnumerable<BreedingEvent>> GetByAnimalIdAsync(int animalId);
        Task<IEnumerable<BreedingEvent>> GetAllAsync(BreedingEventFilterDto filter);
        Task<BreedingEvent?> GetByIdAsync(int id);
        Task<int> CountActiveByAnimalIdAsync(int animalId);
        Task<BreedingEvent?> GetBySyncIdAsync(Guid syncId);
        Task<BreedingEvent> CreateAsync(BreedingEvent breedingEvent, SemenSampleMovement? semenOutput);
        Task<BreedingEvent> UpdateAsync(BreedingEvent breedingEvent);
        Task<BreedingEvent?> SaveDiagnosisAsync(BreedingEvent breedingEvent, AnimalPregnancy? pregnancy);
        Task<SemenSampleMovement?> GetSemenOutputAsync(int breedingEventId);
        Task<IReadOnlyList<BreedingEvent>> GetChangesAsync(ulong since, int take);
        Task<bool> HasActiveByAnimalIdAsync(int animalId);
        Task<BreedingEvent?> GetLastActiveAwaitingDiagnosisAsync(int animalId);
        Task<(int Successful, int Unsuccessful, int Awaiting)> GetStatusCountsAsync(DateTime from, DateTime to);
        Task<(int Successful, int Unsuccessful, int Awaiting)> GetFirstServiceStatusCountsAsync(DateTime from, DateTime to);
        Task<List<(int AnimalId, DateTime BreedingDate)>> GetSuccessfulBreedingsAsync(DateTime from, DateTime to);
    }
}
