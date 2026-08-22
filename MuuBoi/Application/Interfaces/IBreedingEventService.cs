using MuuBoi.Application.DTOs;

namespace MuuBoi.Application.Interfaces
{
    public interface IBreedingEventService
    {
        Task<IEnumerable<BreedingEventListItemDto>> GetByAnimalIdAsync(int animalId);
        Task<IEnumerable<BreedingEventListItemDto>> GetAllAsync(BreedingEventFilterDto filter);
        Task<BreedingEventDto> GetByIdAsync(int id);
        Task<BreedingEventDto> CreateAsync(int animalId, BreedingEventCreateDto dto);
        Task<BreedingEventDto> UpdateAsync(int id, BreedingEventUpdateDto dto);
        Task<BreedingEventDto> UpdateStatusAsync(int id, BreedingEventStatusUpdateDto dto);
        Task DeactivateAsync(int id);
    }
}
