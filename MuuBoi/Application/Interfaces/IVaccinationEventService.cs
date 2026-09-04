using MuuBoi.Application.DTOs;

namespace MuuBoi.Application.Interfaces
{
    public interface IVaccinationEventService
    {
        Task<IEnumerable<VaccinationEventListItemDto>> GetAllAsync(VaccinationEventFilterDto filter);
        Task<VaccinationEventDto> GetByIdAsync(int id);
        Task<VaccinationEventDto> CreateAsync(VaccinationEventCreateDto dto);
        Task<VaccinationEventDto> CreateBoosterAsync(int parentId, VaccinationBoosterCreateDto dto);
        Task<VaccinationEventDto> UpdateAsync(int id, VaccinationEventUpdateDto dto);
        Task<bool> DeactivateAsync(int id);
        Task<IEnumerable<VaccinationHistoryItemDto>> GetAnimalHistoryAsync(int animalId);
    }
}
