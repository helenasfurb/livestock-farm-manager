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
        Task<BreedingEvent> CreateAsync(BreedingEvent breedingEvent);
        Task<BreedingEvent> UpdateAsync(BreedingEvent breedingEvent);
        Task<bool> HasActiveByAnimalIdAsync(int animalId);
        Task<DateTime?> GetLastActiveAwaitingDiagnosisDateAsync(int animalId);
    }
}
