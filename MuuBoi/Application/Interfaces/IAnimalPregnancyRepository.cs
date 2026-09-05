using MuuBoi.Application.DTOs;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Interfaces
{
    public interface IAnimalPregnancyRepository
    {
        Task<IEnumerable<AnimalPregnancy>> GetAllAsync(AnimalPregnancyFilterDto filter);
        Task<IEnumerable<AnimalPregnancy>> GetByAnimalIdAsync(int animalId, bool? isActive);
        Task<AnimalPregnancy?> GetByIdAsync(int id);
        Task<AnimalPregnancy> CreateAsync(AnimalPregnancy pregnancy);
        Task<AnimalPregnancy> UpdateAsync(AnimalPregnancy pregnancy);
        Task<bool> ExistsActiveForBreedingEventAsync(int breedingEventId);
        Task<bool> HasActiveConfirmedByAnimalIdAsync(int animalId);
        Task<AnimalPregnancy?> GetByClientRequestIdAsync(Guid clientRequestId);
    }
}
