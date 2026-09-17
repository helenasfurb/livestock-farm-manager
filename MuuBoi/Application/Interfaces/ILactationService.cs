using MuuBoi.Application.DTOs;

namespace MuuBoi.Application.Interfaces
{
    public interface ILactationService
    {
        Task<IEnumerable<LactationListItemDto>> GetByAnimalIdAsync(int animalId);
        Task<LactationDto?> GetCurrentByAnimalIdAsync(int animalId);
        Task<LactationDto> GetByIdAsync(int id);
        Task<LactationDto> CreateAsync(int animalId, LactationCreateDto dto);
        Task<LactationDto> UpdateAsync(int id, LactationUpdateDto dto);
        Task<LactationDto> DryOffAsync(int id, LactationDryOffDto dto);
        Task<LactationDto> UndoDryOffAsync(int id);
        Task<bool> DeactivateAsync(int id);
    }
}
