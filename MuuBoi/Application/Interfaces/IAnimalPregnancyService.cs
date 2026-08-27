using MuuBoi.Application.DTOs;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Interfaces
{
    public interface IAnimalPregnancyService
    {
        Task<IEnumerable<AnimalPregnancyListItemDto>> GetAllAsync(AnimalPregnancyFilterDto filter);
        Task<IEnumerable<AnimalPregnancyListItemDto>> GetByAnimalIdAsync(int animalId, bool? isActive);
        Task<AnimalPregnancyDto> GetByIdAsync(int id);
        Task<AnimalPregnancyDto> RegisterLossAsync(int id, AnimalPregnancyStatusUpdateDto dto);
        Task<bool> InactivateAsync(int id);
        Task<bool> CreateForBreedingEventAsync(BreedingEvent breedingEvent, DateTime confirmationDate);
    }
}
